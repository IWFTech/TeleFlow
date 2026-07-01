using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TeleFlow.Telegram.Internal;

internal sealed partial class TelegramRequestExecutor : ITelegramRequestExecutor
{
    private const int RequestStartedEventId = 1;
    private const int RequestCompletedEventId = 2;
    private const int RequestThrottledEventId = 3;
    private const int RequestFailedEventId = 4;

    private readonly JsonSerializerOptions _serializerOptions;
    private readonly TelegramRequestSender _sender;
    private readonly TelegramTransportEnvelopeParser _envelopeParser;
    private readonly TelegramRetryAfterPolicy _retryAfterPolicy;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TelegramRequestExecutor> _logger;

    public TelegramRequestExecutor(
        ITelegramTransport transport,
        TelegramClientOptions options,
        TelegramJsonOptions jsonOptions,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(jsonOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _serializerOptions = jsonOptions.SerializerOptions;
        _sender = new TelegramRequestSender(
            transport,
            options,
            new TelegramRequestContentBuilder(_serializerOptions));
        _envelopeParser = new TelegramTransportEnvelopeParser(_serializerOptions);
        _retryAfterPolicy = options.RetryAfter;
        _timeProvider = timeProvider;
        _logger = loggerFactory.CreateLogger<TelegramRequestExecutor>();
    }

    public async Task<TResponse> ExecuteAsync<TResponse>(
        ITelegramRequest<TResponse> request,
        CancellationToken cancellationToken = default)
        where TResponse : ITelegramResponse
    {
        if (request is not ITelegramExecutableRequest<TResponse> executableRequest)
        {
            throw new InvalidOperationException(
                $"Request type '{request.GetType().FullName}' is not executable by the Telegram runtime.");
        }

        var attempt = 0;
        var isPollingRequest = IsPollingRequest(executableRequest.MethodName);

        while (true)
        {
            attempt++;
            TelegramTransportResponse response;
            var requestDebugEnabled = !isPollingRequest && _logger.IsEnabled(LogLevel.Debug);
            var requestErrorEnabled = !isPollingRequest && _logger.IsEnabled(LogLevel.Error);
            var requestTimingEnabled = !isPollingRequest && TelegramHandlerRequestTimingScope.HasCurrent;
            var requestDiagnosticsEnabled = requestDebugEnabled || requestErrorEnabled || requestTimingEnabled;
            var attemptStarted = requestDiagnosticsEnabled ? _timeProvider.GetTimestamp() : 0;

            try
            {
                var transportRequest = _sender.CreateRequest(executableRequest);

                if (requestDebugEnabled)
                {
                    LogRequestStarted(
                        _logger,
                        executableRequest.MethodName,
                        attempt,
                        GetContentKind(transportRequest.Content));
                }

                response = await _sender.SendAsync(transportRequest, cancellationToken).ConfigureAwait(false);

                if (requestDiagnosticsEnabled)
                {
                    var attemptEnded = _timeProvider.GetTimestamp();

                    if (requestTimingEnabled)
                    {
                        TelegramHandlerRequestTimingScope.Record(attemptStarted, attemptEnded);
                    }

                    if (requestDebugEnabled)
                    {
                        LogRequestCompleted(
                            _logger,
                            executableRequest.MethodName,
                            attempt,
                            response.StatusCode,
                            GetElapsedMilliseconds(attemptStarted, attemptEnded));
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var httpStatusCode = exception is TelegramRequestException requestException
                    ? requestException.HttpStatusCode
                    : null;
                if (requestDiagnosticsEnabled)
                {
                    var attemptEnded = _timeProvider.GetTimestamp();

                    if (requestTimingEnabled)
                    {
                        TelegramHandlerRequestTimingScope.Record(attemptStarted, attemptEnded);
                    }

                    if (requestErrorEnabled)
                    {
                        LogRequestFailure(
                            executableRequest.MethodName,
                            attempt,
                            httpStatusCode,
                            attemptStarted,
                            attemptEnded,
                            exception);
                    }
                }

                throw;
            }

            var envelope = _envelopeParser.TryParse(response.Body, out var parsedEnvelope, out var parseException)
                ? parsedEnvelope
                : null;

            if (envelope is null)
            {
                var retryAfterHeaderDelay = TelegramRetryAfterDelayResolver.ResolveDelay(response, envelope, _timeProvider);
                if (response.StatusCode == 429)
                {
                    if (retryAfterHeaderDelay is not null &&
                        CanRetryAfter(attempt, retryAfterHeaderDelay.Value))
                    {
                        if (!isPollingRequest && _logger.IsEnabled(LogLevel.Warning))
                        {
                            LogRequestThrottled(executableRequest.MethodName, attempt, retryAfterHeaderDelay.Value);
                        }

                        await TelegramRetryAfterDelayResolver.DelayAsync(
                            retryAfterHeaderDelay.Value,
                            _timeProvider,
                            cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var exception = CreateRetryAfterException(
                        executableRequest.MethodName,
                        response.StatusCode,
                        envelope: null,
                        retryAfterHeaderDelay,
                        GetRetryAfterFailureDescription(retryAfterHeaderDelay));
                    if (requestErrorEnabled)
                    {
                        LogRequestFailure(
                            executableRequest.MethodName,
                            attempt,
                            response.StatusCode,
                            attemptStarted,
                            _timeProvider.GetTimestamp(),
                            exception);
                    }

                    throw exception;
                }

                var decodeException = new TelegramDecodeException(
                    $"Failed to parse Telegram API response envelope for method '{executableRequest.MethodName}'.",
                    parseException,
                    executableRequest.MethodName,
                    httpStatusCode: response.StatusCode,
                    retryAfterHeaderDelay?.Seconds);
                if (requestErrorEnabled)
                {
                    LogRequestFailure(
                        executableRequest.MethodName,
                        attempt,
                        response.StatusCode,
                        attemptStarted,
                        _timeProvider.GetTimestamp(),
                        decodeException);
                }

                throw decodeException;
            }

            if (envelope.Ok)
            {
                if (string.IsNullOrWhiteSpace(envelope.ResultJson))
                {
                    var missingResultException = new TelegramDecodeException(
                        $"Telegram response for method '{executableRequest.MethodName}' did not contain a result payload.",
                        methodName: executableRequest.MethodName,
                        httpStatusCode: response.StatusCode);
                    if (requestErrorEnabled)
                    {
                        LogRequestFailure(
                            executableRequest.MethodName,
                            attempt,
                            response.StatusCode,
                            attemptStarted,
                            _timeProvider.GetTimestamp(),
                            missingResultException);
                    }

                    throw missingResultException;
                }

                try
                {
                    return executableRequest.DeserializeResponse(_serializerOptions, envelope.ResultJson);
                }
                catch (TelegramRequestException)
                {
                    throw;
                }
                catch (Exception exception) when (exception is JsonException or NotSupportedException)
                {
                    var decodeException = new TelegramDecodeException(
                        $"Failed to deserialize Telegram response for method '{executableRequest.MethodName}'.",
                        exception,
                        executableRequest.MethodName,
                        httpStatusCode: response.StatusCode);
                    if (requestErrorEnabled)
                    {
                        LogRequestFailure(
                            executableRequest.MethodName,
                            attempt,
                            response.StatusCode,
                            attemptStarted,
                            _timeProvider.GetTimestamp(),
                            decodeException);
                    }

                    throw decodeException;
                }
            }

            var retryAfterDelay = TelegramRetryAfterDelayResolver.ResolveDelay(response, envelope, _timeProvider);
            if (TelegramApiExceptionFactory.IsThrottling(response.StatusCode, envelope) &&
                retryAfterDelay is not null &&
                CanRetryAfter(attempt, retryAfterDelay.Value))
            {
                if (!isPollingRequest && _logger.IsEnabled(LogLevel.Warning))
                {
                    LogRequestThrottled(executableRequest.MethodName, attempt, retryAfterDelay.Value);
                }

                await TelegramRetryAfterDelayResolver.DelayAsync(
                    retryAfterDelay.Value,
                    _timeProvider,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (TelegramApiExceptionFactory.IsThrottling(response.StatusCode, envelope))
            {
                var retryAfterException = CreateRetryAfterException(
                    executableRequest.MethodName,
                    response.StatusCode,
                    envelope,
                    retryAfterDelay,
                    GetRetryAfterFailureDescription(retryAfterDelay));
                if (requestErrorEnabled)
                {
                    LogRequestFailure(
                        executableRequest.MethodName,
                        attempt,
                        response.StatusCode,
                        attemptStarted,
                        _timeProvider.GetTimestamp(),
                        retryAfterException);
                }

                throw retryAfterException;
            }

            var apiException = TelegramApiExceptionFactory.CreateApiException(
                executableRequest.MethodName,
                response.StatusCode,
                envelope);
            if (requestErrorEnabled)
            {
                LogRequestFailure(
                    executableRequest.MethodName,
                    attempt,
                    response.StatusCode,
                    attemptStarted,
                    _timeProvider.GetTimestamp(),
                    apiException);
            }

            throw apiException;
        }
    }

    private static bool IsPollingRequest(string methodName)
    {
        return string.Equals(methodName, "getUpdates", StringComparison.Ordinal);
    }

    private bool CanRetryAfter(int attempt, TelegramRetryAfterDelay retryAfter)
    {
        return _retryAfterPolicy.Enabled &&
            attempt <= _retryAfterPolicy.MaxRetries &&
            retryAfter.Value <= _retryAfterPolicy.MaxDelay;
    }

    private static TelegramRetryAfterException CreateRetryAfterException(
        string methodName,
        int httpStatusCode,
        TelegramTransportEnvelope? envelope,
        TelegramRetryAfterDelay? retryAfter,
        string fallbackDescription)
    {
        var description = envelope?.Description ?? fallbackDescription;
        var message = $"Telegram request '{methodName}' failed: {description}";

        return new TelegramRetryAfterException(
            message,
            methodName,
            httpStatusCode: httpStatusCode,
            telegramErrorCode: envelope?.ErrorCode,
            description: description,
            retryAfterSeconds: retryAfter?.Seconds);
    }

    private static string GetRetryAfterFailureDescription(TelegramRetryAfterDelay? retryAfter)
    {
        return retryAfter is null
            ? "Telegram throttling response did not provide retry timing metadata."
            : "Telegram request was throttled and automatic retry-after handling was not applied by the configured policy.";
    }

    private static string GetContentKind(TelegramTransportContent content)
    {
        return content switch
        {
            TelegramJsonTransportContent => "json",
            TelegramMultipartTransportContent => "multipart",
            _ => content.GetType().Name
        };
    }

    private void LogRequestThrottled(
        string methodName,
        int attempt,
        TelegramRetryAfterDelay retryAfter)
    {
        LogRequestThrottledCore(
            _logger,
            methodName,
            attempt,
            retryAfter.Value);
    }

    private void LogRequestFailure(
        string methodName,
        int attempt,
        int? httpStatusCode,
        long attemptStarted,
        long attemptEnded,
        Exception exception)
    {
        LogRequestFailed(
            _logger,
            exception,
            methodName,
            attempt,
            httpStatusCode,
            GetElapsedMilliseconds(attemptStarted, attemptEnded),
            exception.GetType().FullName ?? exception.GetType().Name);
    }

    private double GetElapsedMilliseconds(long startingTimestamp, long endingTimestamp)
    {
        return _timeProvider.GetElapsedTime(startingTimestamp, endingTimestamp).TotalMilliseconds;
    }

    [LoggerMessage(
        EventId = RequestStartedEventId,
        Level = LogLevel.Debug,
        Message = "Telegram request started. method={MethodName}, attempt={Attempt}, content={ContentKind}.")]
    private static partial void LogRequestStarted(
        ILogger logger,
        string methodName,
        int attempt,
        string contentKind);

    [LoggerMessage(
        EventId = RequestCompletedEventId,
        Level = LogLevel.Debug,
        Message = "Telegram request completed. method={MethodName}, attempt={Attempt}, status={HttpStatusCode}, request_ms={RequestElapsedMilliseconds:F2}.")]
    private static partial void LogRequestCompleted(
        ILogger logger,
        string methodName,
        int attempt,
        int httpStatusCode,
        double requestElapsedMilliseconds);

    [LoggerMessage(
        EventId = RequestThrottledEventId,
        Level = LogLevel.Warning,
        Message = "Telegram request throttled. method={MethodName}, attempt={Attempt}, retry_after={RetryAfter}.")]
    private static partial void LogRequestThrottledCore(
        ILogger logger,
        string methodName,
        int attempt,
        TimeSpan retryAfter);

    [LoggerMessage(
        EventId = RequestFailedEventId,
        Level = LogLevel.Error,
        Message = "Telegram request failed. method={MethodName}, attempt={Attempt}, status={HttpStatusCode}, request_ms={RequestElapsedMilliseconds:F2}, exception_type={ExceptionType}.")]
    private static partial void LogRequestFailed(
        ILogger logger,
        Exception exception,
        string methodName,
        int attempt,
        int? httpStatusCode,
        double requestElapsedMilliseconds,
        string exceptionType);
}
