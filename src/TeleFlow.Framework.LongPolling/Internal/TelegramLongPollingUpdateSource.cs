using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using TeleFlow.Framework.Updates;
using TeleFlow.Telegram.Internal.Handlers;

namespace TeleFlow.Telegram.Internal;

[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "The type is instantiated by dependency injection through AddLongPolling.")]
internal sealed partial class TelegramLongPollingUpdateSource : IUpdateSource
{
    private readonly ITelegramLongPollingClient _pollingClient;
    private readonly ITelegramClient _bot;
    private readonly TelegramBotIdentity _botIdentity;
    private readonly TelegramLongPollingOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TelegramLongPollingUpdateSource> _logger;
    private readonly IReadOnlyList<TelegramHandlerDescriptor> _handlerDescriptors;

    public TelegramLongPollingUpdateSource(
        ITelegramLongPollingClient pollingClient,
        ITelegramClient bot,
        TelegramBotIdentity botIdentity,
        TelegramLongPollingOptions options,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IEnumerable<TelegramHandlerDescriptor> handlerDescriptors)
    {
        ArgumentNullException.ThrowIfNull(pollingClient);
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(botIdentity);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(handlerDescriptors);

        _pollingClient = pollingClient;
        _bot = bot;
        _botIdentity = botIdentity;
        _options = options;
        _timeProvider = timeProvider;
        _logger = loggerFactory.CreateLogger<TelegramLongPollingUpdateSource>();
        _handlerDescriptors = handlerDescriptors.ToArray();
    }

    public async Task StartAsync(
        Func<IUpdatePayload, CancellationToken, Task> updateHandler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updateHandler);

        await _botIdentity.EnsureResolvedAsync(_bot, cancellationToken).ConfigureAwait(false);

        var allowedUpdates = TelegramAllowedUpdatesResolver.Resolve(_options.AllowedUpdates, _handlerDescriptors, _logger);
        var rawOptions = CreateRawOptions(allowedUpdates);
        var connected = false;

        LogStarting(
            _logger,
            TelegramUpdateLogFormatter.FormatAllowedUpdates(allowedUpdates),
            _options.TimeoutSeconds,
            _options.Limit);

        try
        {
            await foreach (var item in _pollingClient.GetUpdatesAsync(rawOptions, cancellationToken).ConfigureAwait(false))
            {
                if (!connected)
                {
                    LogConnected(_logger);
                    connected = true;
                }

                var update = item.Update;
                var informationEnabled = _logger.IsEnabled(LogLevel.Information);
                var errorEnabled = _logger.IsEnabled(LogLevel.Error);
                var debugEnabled = _logger.IsEnabled(LogLevel.Debug);
                string? updateType = null;
                string GetUpdateType() => updateType ??= TelegramUpdateLogFormatter.GetUpdateType(update);
                var processingStarted = debugEnabled ? _timeProvider.GetTimestamp() : 0;

                if (informationEnabled)
                {
                    LogUpdateReceived(
                        _logger,
                        update.UpdateId,
                        GetUpdateType(),
                        item.BatchIndex,
                        item.BatchCount);
                }

                try
                {
                    await updateHandler(new TelegramUpdatePayload(update), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (!IsUpdateCancellation(exception, cancellationToken))
                {
                    if (errorEnabled)
                    {
                        if (debugEnabled)
                        {
                            LogUpdateProcessingFailedWithTiming(
                                _logger,
                                exception,
                                update.UpdateId,
                                GetUpdateType(),
                                GetElapsedMilliseconds(processingStarted));
                        }
                        else
                        {
                            LogUpdateProcessingFailed(
                                _logger,
                                exception,
                                update.UpdateId,
                                GetUpdateType());
                        }
                    }

                    throw;
                }

                await item.AcknowledgeAsync(CancellationToken.None).ConfigureAwait(false);

                if (informationEnabled)
                {
                    LogUpdateProcessed(
                        _logger,
                        update.UpdateId,
                        GetUpdateType());
                }

                if (debugEnabled)
                {
                    LogUpdateProcessedWithTiming(
                        _logger,
                        update.UpdateId,
                        GetUpdateType(),
                        GetElapsedMilliseconds(processingStarted));
                }
            }
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
            {
                LogStopped(_logger);
            }
        }
    }

    private TelegramRawLongPollingOptions CreateRawOptions(IReadOnlyList<string>? allowedUpdates)
    {
        return new TelegramRawLongPollingOptions
        {
            TimeoutSeconds = _options.TimeoutSeconds,
            Limit = _options.Limit,
            AllowedUpdates = allowedUpdates,
            Backoff = new TelegramRawLongPollingBackoffOptions
            {
                Enabled = _options.Backoff.Enabled,
                MinDelay = _options.Backoff.MinDelay,
                MaxDelay = _options.Backoff.MaxDelay,
                Factor = _options.Backoff.Factor,
                Jitter = _options.Backoff.Jitter
            }
        };
    }

    private double GetElapsedMilliseconds(long startingTimestamp)
    {
        return _timeProvider.GetElapsedTime(startingTimestamp).TotalMilliseconds;
    }

    private static bool IsUpdateCancellation(Exception exception, CancellationToken cancellationToken)
    {
        return exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Starting Telegram long polling. allowed_updates={AllowedUpdates}, timeout={TimeoutSeconds}s, limit={Limit}.")]
    private static partial void LogStarting(
        ILogger logger,
        string allowedUpdates,
        int timeoutSeconds,
        int limit);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Telegram long polling connected.")]
    private static partial void LogConnected(ILogger logger);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Telegram update received. update_id={UpdateId}, type={UpdateType}, batch_index={BatchIndex}/{BatchCount}.")]
    private static partial void LogUpdateReceived(
        ILogger logger,
        long updateId,
        string updateType,
        int batchIndex,
        int batchCount);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Telegram update processing failed. update_id={UpdateId}, type={UpdateType}.")]
    private static partial void LogUpdateProcessingFailed(
        ILogger logger,
        Exception exception,
        long updateId,
        string updateType);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Telegram update processing failed. update_id={UpdateId}, type={UpdateType}, total_ms={TotalElapsedMilliseconds:F2}.")]
    private static partial void LogUpdateProcessingFailedWithTiming(
        ILogger logger,
        Exception exception,
        long updateId,
        string updateType,
        double totalElapsedMilliseconds);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Telegram update processed. update_id={UpdateId}, type={UpdateType}.")]
    private static partial void LogUpdateProcessed(
        ILogger logger,
        long updateId,
        string updateType);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Debug,
        Message = "Telegram update processed. update_id={UpdateId}, type={UpdateType}, total_ms={TotalElapsedMilliseconds:F2}.")]
    private static partial void LogUpdateProcessedWithTiming(
        ILogger logger,
        long updateId,
        string updateType,
        double totalElapsedMilliseconds);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Telegram long polling stopped.")]
    private static partial void LogStopped(ILogger logger);
}
