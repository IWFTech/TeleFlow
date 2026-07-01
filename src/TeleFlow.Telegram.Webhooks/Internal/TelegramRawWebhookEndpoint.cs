using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Webhooks.Internal;

/// <summary>
/// Handles raw ASP.NET Core webhook requests, validates Telegram webhook security metadata,
/// deserializes updates, and passes accepted updates to the user-provided webhook handler.
/// </summary>
internal sealed partial class TelegramRawWebhookEndpoint
{
    private const string SecretTokenHeaderName = "X-Telegram-Bot-Api-Secret-Token";

    private const int SecretTokenRejectedEventId = 1;
    private const int InvalidPayloadRejectedEventId = 2;
    private const int UpdateReceivedEventId = 3;
    private const int UpdateProcessedEventId = 4;
    private const int UpdateProcessingFailedEventId = 5;

    private static readonly TelegramJsonOptions DefaultJsonOptions = TelegramJsonOptions.CreateDefault();

    private readonly TelegramRawWebhookHandler _handler;
    private readonly TelegramRawWebhookOptions _options;
    private readonly ILogger<TelegramRawWebhookEndpoint>? _logger;

    public TelegramRawWebhookEndpoint(
        TelegramRawWebhookHandler handler,
        TelegramRawWebhookOptions options,
        ILogger<TelegramRawWebhookEndpoint>? logger = null)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task<IResult> HandleAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!IsSecretTokenAccepted(context))
        {
            if (_logger is not null)
            {
                LogSecretTokenRejected(_logger, _options.SecretTokenFailureStatusCode);
            }

            return Results.StatusCode(_options.SecretTokenFailureStatusCode);
        }

        var jsonOptions = context.RequestServices.GetService<TelegramJsonOptions>() ?? DefaultJsonOptions;
        Update? update;

        try
        {
            update = await JsonSerializer.DeserializeAsync<Update>(
                context.Request.Body,
                jsonOptions.SerializerOptions,
                context.RequestAborted).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            if (_logger is not null)
            {
                LogInvalidPayloadRejected(_logger, _options.InvalidPayloadStatusCode);
            }

            return Results.StatusCode(_options.InvalidPayloadStatusCode);
        }

        if (update is null)
        {
            if (_logger is not null)
            {
                LogInvalidPayloadRejected(_logger, _options.InvalidPayloadStatusCode);
            }

            return Results.StatusCode(_options.InvalidPayloadStatusCode);
        }

        string? updateType = null;
        string GetUpdateType()
        {
            return updateType ??= TelegramWebhookUpdateLogFormatter.GetUpdateType(update);
        }

        if (_logger?.IsEnabled(LogLevel.Debug) == true)
        {
            LogUpdateReceived(_logger, update.UpdateId, GetUpdateType());
        }

        var bot = context.RequestServices.GetRequiredService<ITelegramClient>();
        try
        {
            var result = await _handler(update, bot, context.RequestAborted).ConfigureAwait(false);

            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                LogUpdateProcessed(_logger, update.UpdateId, GetUpdateType());
            }

            return result;
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
            {
                LogUpdateProcessingFailed(_logger, exception, update.UpdateId, GetUpdateType());
            }

            throw;
        }
    }

    private bool IsSecretTokenAccepted(HttpContext context)
    {
        if (_options.SecretToken is null)
        {
            return true;
        }

        return context.Request.Headers.TryGetValue(SecretTokenHeaderName, out var values) &&
            values.Count == 1 &&
            string.Equals(values[0], _options.SecretToken, StringComparison.Ordinal);
    }

    [LoggerMessage(
        EventId = SecretTokenRejectedEventId,
        Level = LogLevel.Warning,
        Message = "Telegram webhook request rejected because secret token validation failed. status={StatusCode}.")]
    private static partial void LogSecretTokenRejected(
        ILogger logger,
        int statusCode);

    [LoggerMessage(
        EventId = InvalidPayloadRejectedEventId,
        Level = LogLevel.Warning,
        Message = "Telegram webhook request rejected because payload was invalid. status={StatusCode}.")]
    private static partial void LogInvalidPayloadRejected(
        ILogger logger,
        int statusCode);

    [LoggerMessage(
        EventId = UpdateReceivedEventId,
        Level = LogLevel.Debug,
        Message = "Telegram webhook update received. update_id={UpdateId}, type={UpdateType}.")]
    private static partial void LogUpdateReceived(
        ILogger logger,
        long updateId,
        string updateType);

    [LoggerMessage(
        EventId = UpdateProcessedEventId,
        Level = LogLevel.Debug,
        Message = "Telegram webhook update processed. update_id={UpdateId}, type={UpdateType}.")]
    private static partial void LogUpdateProcessed(
        ILogger logger,
        long updateId,
        string updateType);

    [LoggerMessage(
        EventId = UpdateProcessingFailedEventId,
        Level = LogLevel.Error,
        Message = "Telegram webhook update processing failed. update_id={UpdateId}, type={UpdateType}.")]
    private static partial void LogUpdateProcessingFailed(
        ILogger logger,
        Exception exception,
        long updateId,
        string updateType);
}
