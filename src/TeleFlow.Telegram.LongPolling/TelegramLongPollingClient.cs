using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Schema.Methods;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public sealed partial class TelegramLongPollingClient : ITelegramLongPollingClient
{
    private readonly ITelegramClient _telegramClient;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TelegramLongPollingClient> _logger;

    public TelegramLongPollingClient(
        ITelegramClient telegramClient,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(telegramClient);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _telegramClient = telegramClient;
        _timeProvider = timeProvider;
        _logger = loggerFactory.CreateLogger<TelegramLongPollingClient>();
    }

    public async Task RunAsync(
        Func<Update, CancellationToken, Task> updateHandler,
        TelegramRawLongPollingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updateHandler);

        options ??= new TelegramRawLongPollingOptions();
        TelegramRawLongPollingOptionsValidator.Validate(options);

        long? offset = null;
        var backoff = new TelegramRawLongPollingBackoff(options.Backoff);
        var allowedUpdates = CopyAllowedUpdates(options.AllowedUpdates);
        var connected = false;
        var recoveringFromPollingFailure = false;

        LogStart(options, allowedUpdates);

        while (!cancellationToken.IsCancellationRequested)
        {
            var updates = await GetUpdatesBatchAsync(
                offset,
                options,
                allowedUpdates,
                backoff,
                () =>
                {
                    if (!connected)
                    {
                        LogConnected(_logger);
                        connected = true;
                    }

                    if (recoveringFromPollingFailure)
                    {
                        LogGetUpdatesRecovered(_logger);
                        recoveringFromPollingFailure = false;
                    }
                },
                () => recoveringFromPollingFailure = true,
                cancellationToken).ConfigureAwait(false);

            for (var index = 0; index < updates.Count; index++)
            {
                var update = updates[index];

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var updateType = TelegramRawLongPollingLogFormatter.GetUpdateType(update);
                    var processingStarted = _timeProvider.GetTimestamp();

                    LogUpdateReceived(
                        _logger,
                        update.UpdateId,
                        updateType,
                        index + 1,
                        updates.Count);

                    await updateHandler(update, cancellationToken).ConfigureAwait(false);
                    offset = update.UpdateId + 1;

                    LogUpdateAcknowledgedByHandler(
                        _logger,
                        update.UpdateId,
                        updateType,
                        GetElapsedMilliseconds(processingStarted));
                    continue;
                }

                await updateHandler(update, cancellationToken).ConfigureAwait(false);
                offset = update.UpdateId + 1;
            }
        }
    }

    public async IAsyncEnumerable<TelegramPolledUpdate> GetUpdatesAsync(
        TelegramRawLongPollingOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new TelegramRawLongPollingOptions();
        TelegramRawLongPollingOptionsValidator.Validate(options);

        long? offset = null;
        var backoff = new TelegramRawLongPollingBackoff(options.Backoff);
        var allowedUpdates = CopyAllowedUpdates(options.AllowedUpdates);
        var connected = false;
        var recoveringFromPollingFailure = false;

        LogStart(options, allowedUpdates);

        while (!cancellationToken.IsCancellationRequested)
        {
            var updates = await GetUpdatesBatchAsync(
                offset,
                options,
                allowedUpdates,
                backoff,
                () =>
                {
                    if (!connected)
                    {
                        LogConnected(_logger);
                        connected = true;
                    }

                    if (recoveringFromPollingFailure)
                    {
                        LogGetUpdatesRecovered(_logger);
                        recoveringFromPollingFailure = false;
                    }
                },
                () => recoveringFromPollingFailure = true,
                cancellationToken).ConfigureAwait(false);

            for (var index = 0; index < updates.Count; index++)
            {
                var update = updates[index];
                var polledUpdate = new TelegramPolledUpdate(update, index + 1, updates.Count);

                var debugEnabled = _logger.IsEnabled(LogLevel.Debug);
                var updateType = debugEnabled
                    ? TelegramRawLongPollingLogFormatter.GetUpdateType(update)
                    : string.Empty;

                if (debugEnabled)
                {
                    LogStreamUpdateReceived(
                        _logger,
                        update.UpdateId,
                        updateType);
                }

                yield return polledUpdate;

                if (!polledUpdate.IsAcknowledged)
                {
                    throw new InvalidOperationException(
                        "Telegram polled updates must be acknowledged with AcknowledgeAsync before requesting the next update.");
                }

                offset = update.UpdateId + 1;

                if (debugEnabled)
                {
                    LogStreamUpdateAcknowledged(
                        _logger,
                        update.UpdateId,
                        updateType);
                }
            }
        }
    }

    private async Task<IReadOnlyList<Update>> GetUpdatesBatchAsync(
        long? offset,
        TelegramRawLongPollingOptions options,
        IReadOnlyList<string>? allowedUpdates,
        TelegramRawLongPollingBackoff backoff,
        Action onSuccess,
        Action onTransientFailure,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                var updates = await _telegramClient.SendAsync(
                    new GetUpdates
                    {
                        Offset = offset,
                        Limit = options.Limit,
                        Timeout = options.TimeoutSeconds,
                        AllowedUpdates = allowedUpdates
                    },
                    cancellationToken).ConfigureAwait(false);

                onSuccess();
                backoff.Reset();
                return updates;
            }
            catch (Exception exception) when (IsPollingTransient(exception) && !cancellationToken.IsCancellationRequested)
            {
                var delay = GetPollingRetryDelay(exception, backoff);
                onTransientFailure();

                LogGetUpdatesFailed(
                    _logger,
                    exception,
                    delay);

                await DelayAsync(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void LogStart(
        TelegramRawLongPollingOptions options,
        IReadOnlyList<string>? allowedUpdates)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        LogStarting(
            _logger,
            TelegramRawLongPollingLogFormatter.FormatAllowedUpdates(allowedUpdates),
            options.TimeoutSeconds,
            options.Limit);
    }

    private double GetElapsedMilliseconds(long startingTimestamp)
    {
        return _timeProvider.GetElapsedTime(startingTimestamp).TotalMilliseconds;
    }

    private ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return delay <= TimeSpan.Zero
            ? ValueTask.CompletedTask
            : new ValueTask(Task.Delay(delay, _timeProvider, cancellationToken));
    }

    private static string[]? CopyAllowedUpdates(IReadOnlyList<string>? allowedUpdates)
    {
        return allowedUpdates is null ? null : allowedUpdates.ToArray();
    }

    private static bool IsPollingTransient(Exception exception)
    {
        return exception is TelegramNetworkException or
            TelegramServerException or
            TelegramDecodeException or
            TelegramRetryAfterException;
    }

    private static TimeSpan GetPollingRetryDelay(Exception exception, TelegramRawLongPollingBackoff backoff)
    {
        return exception is TelegramRetryAfterException { RetryAfter: { } retryAfter }
            ? retryAfter
            : backoff.NextDelay();
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Starting raw Telegram long polling. allowed_updates={AllowedUpdates}, timeout={TimeoutSeconds}s, limit={Limit}.")]
    private static partial void LogStarting(
        ILogger logger,
        string allowedUpdates,
        int timeoutSeconds,
        int limit);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Raw Telegram long polling connected.")]
    private static partial void LogConnected(ILogger logger);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Raw Telegram long polling getUpdates recovered after transient failures.")]
    private static partial void LogGetUpdatesRecovered(ILogger logger);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "Raw Telegram update received. update_id={UpdateId}, type={UpdateType}, batch_index={BatchIndex}/{BatchCount}.")]
    private static partial void LogUpdateReceived(
        ILogger logger,
        long updateId,
        string updateType,
        int batchIndex,
        int batchCount);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Debug,
        Message = "Raw Telegram update acknowledged by handler. update_id={UpdateId}, type={UpdateType}, total_ms={TotalElapsedMilliseconds:F2}.")]
    private static partial void LogUpdateAcknowledgedByHandler(
        ILogger logger,
        long updateId,
        string updateType,
        double totalElapsedMilliseconds);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Debug,
        Message = "Raw Telegram update received. update_id={UpdateId}, type={UpdateType}.")]
    private static partial void LogStreamUpdateReceived(
        ILogger logger,
        long updateId,
        string updateType);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Debug,
        Message = "Raw Telegram update acknowledged. update_id={UpdateId}, type={UpdateType}.")]
    private static partial void LogStreamUpdateAcknowledged(
        ILogger logger,
        long updateId,
        string updateType);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Warning,
        Message = "Raw Telegram long polling getUpdates failed. Retrying in {Delay}.")]
    private static partial void LogGetUpdatesFailed(
        ILogger logger,
        Exception exception,
        TimeSpan delay);
}
