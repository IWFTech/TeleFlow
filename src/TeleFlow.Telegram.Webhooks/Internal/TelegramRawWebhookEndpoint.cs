using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Webhooks.Internal;

internal sealed class TelegramRawWebhookEndpoint
{
    private const string SecretTokenHeaderName = "X-Telegram-Bot-Api-Secret-Token";

    private static readonly TelegramJsonOptions DefaultJsonOptions = TelegramJsonOptions.CreateDefault();

    private readonly TelegramRawWebhookHandler _handler;
    private readonly TelegramRawWebhookOptions _options;

    public TelegramRawWebhookEndpoint(
        TelegramRawWebhookHandler handler,
        TelegramRawWebhookOptions options)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IResult> HandleAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!IsSecretTokenAccepted(context))
        {
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
            return Results.StatusCode(_options.InvalidPayloadStatusCode);
        }

        if (update is null)
        {
            return Results.StatusCode(_options.InvalidPayloadStatusCode);
        }

        var bot = context.RequestServices.GetRequiredService<ITelegramClient>();
        return await _handler(update, bot, context.RequestAborted).ConfigureAwait(false);
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
}
