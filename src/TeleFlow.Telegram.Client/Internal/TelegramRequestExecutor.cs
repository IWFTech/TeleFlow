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
        var executableRequest = GetExecutableRequest(request);
        var context = TelegramRequestExecutionContext.Create(executableRequest.MethodName);

        for (var attempt = 1; ; attempt++)
        {
            var diagnostics = TelegramRequestAttemptDiagnostics.Create(this, context, attempt);
            var response = await SendAttemptAsync(
                executableRequest,
                diagnostics,
                cancellationToken).ConfigureAwait(false);

            if (!_envelopeParser.TryParse(response.Body, out var envelope, out var parseException))
            {
                if (await TryDelayRetryAfterAsync(
                        diagnostics,
                        attempt,
                        response,
                        envelope: null,
                        cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                throw CreateUnparsedResponseException(
                    context,
                    diagnostics,
                    response,
                    parseException);
            }

            if (envelope.Ok)
            {
                return DeserializeSuccessResponse(
                    executableRequest,
                    context,
                    diagnostics,
                    response,
                    envelope);
            }

            if (await TryDelayRetryAfterAsync(
                    diagnostics,
                    attempt,
                    response,
                    envelope,
                    cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            throw CreateApiFailureException(
                context,
                diagnostics,
                response,
                envelope);
        }
    }

    private static ITelegramExecutableRequest<TResponse> GetExecutableRequest<TResponse>(
        ITelegramRequest<TResponse> request)
        where TResponse : ITelegramResponse
    {
        if (request is ITelegramExecutableRequest<TResponse> executableRequest)
        {
            return executableRequest;
        }

        throw new InvalidOperationException(
            $"Request type '{request.GetType().FullName}' is not executable by the Telegram runtime.");
    }

    private async Task<TelegramTransportResponse> SendAttemptAsync<TResponse>(
        ITelegramExecutableRequest<TResponse> executableRequest,
        TelegramRequestAttemptDiagnostics diagnostics,
        CancellationToken cancellationToken)
        where TResponse : ITelegramResponse
    {
        try
        {
            var transportRequest = _sender.CreateRequest(executableRequest);
            diagnostics.LogStarted(transportRequest.Content);

            var response = await _sender.SendAsync(transportRequest, cancellationToken).ConfigureAwait(false);
            diagnostics.LogCompleted(response.StatusCode);

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            diagnostics.LogAttemptFailure(GetHttpStatusCode(exception), exception);
            throw;
        }
    }

    private async ValueTask<bool> TryDelayRetryAfterAsync(
        TelegramRequestAttemptDiagnostics diagnostics,
        int attempt,
        TelegramTransportResponse response,
        TelegramTransportEnvelope? envelope,
        CancellationToken cancellationToken)
    {
        if (!IsThrottlingResponse(response.StatusCode, envelope))
        {
            return false;
        }

        var retryAfterDelay = TelegramRetryAfterDelayResolver.ResolveDelay(response, envelope, _timeProvider);
        if (retryAfterDelay is null ||
            !CanRetryAfter(attempt, retryAfterDelay.Value))
        {
            return false;
        }

        diagnostics.LogThrottled(retryAfterDelay.Value);

        await TelegramRetryAfterDelayResolver.DelayAsync(
            retryAfterDelay.Value,
            _timeProvider,
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    private TResponse DeserializeSuccessResponse<TResponse>(
        ITelegramExecutableRequest<TResponse> executableRequest,
        TelegramRequestExecutionContext context,
        TelegramRequestAttemptDiagnostics diagnostics,
        TelegramTransportResponse response,
        TelegramTransportEnvelope envelope)
        where TResponse : ITelegramResponse
    {
        if (string.IsNullOrWhiteSpace(envelope.ResultJson))
        {
            var exception = new TelegramDecodeException(
                $"Telegram response for method '{context.MethodName}' did not contain a result payload.",
                methodName: context.MethodName,
                httpStatusCode: response.StatusCode);

            diagnostics.LogFinalFailure(response.StatusCode, exception);
            throw exception;
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
                $"Failed to deserialize Telegram response for method '{context.MethodName}'.",
                exception,
                context.MethodName,
                httpStatusCode: response.StatusCode);

            diagnostics.LogFinalFailure(response.StatusCode, decodeException);
            throw decodeException;
        }
    }

    private Exception CreateUnparsedResponseException(
        TelegramRequestExecutionContext context,
        TelegramRequestAttemptDiagnostics diagnostics,
        TelegramTransportResponse response,
        JsonException? parseException)
    {
        var retryAfterHeaderDelay = TelegramRetryAfterDelayResolver.ResolveDelay(response, envelope: null, _timeProvider);

        if (response.StatusCode == 429)
        {
            var exception = CreateRetryAfterException(
                context.MethodName,
                response.StatusCode,
                envelope: null,
                retryAfterHeaderDelay,
                GetRetryAfterFailureDescription(retryAfterHeaderDelay));

            diagnostics.LogFinalFailure(response.StatusCode, exception);
            return exception;
        }

        var decodeException = new TelegramDecodeException(
            $"Failed to parse Telegram API response envelope for method '{context.MethodName}'.",
            parseException,
            context.MethodName,
            httpStatusCode: response.StatusCode,
            retryAfterHeaderDelay?.Seconds);

        diagnostics.LogFinalFailure(response.StatusCode, decodeException);
        return decodeException;
    }

    private Exception CreateApiFailureException(
        TelegramRequestExecutionContext context,
        TelegramRequestAttemptDiagnostics diagnostics,
        TelegramTransportResponse response,
        TelegramTransportEnvelope envelope)
    {
        if (TelegramApiExceptionFactory.IsThrottling(response.StatusCode, envelope))
        {
            var retryAfterDelay = TelegramRetryAfterDelayResolver.ResolveDelay(response, envelope, _timeProvider);
            var exception = CreateRetryAfterException(
                context.MethodName,
                response.StatusCode,
                envelope,
                retryAfterDelay,
                GetRetryAfterFailureDescription(retryAfterDelay));

            diagnostics.LogFinalFailure(response.StatusCode, exception);
            return exception;
        }

        var apiException = TelegramApiExceptionFactory.CreateApiException(
            context.MethodName,
            response.StatusCode,
            envelope);

        diagnostics.LogFinalFailure(response.StatusCode, apiException);
        return apiException;
    }

    private static bool IsThrottlingResponse(
        int statusCode,
        TelegramTransportEnvelope? envelope)
    {
        return envelope is null
            ? statusCode == 429
            : TelegramApiExceptionFactory.IsThrottling(statusCode, envelope);
    }

    private static int? GetHttpStatusCode(Exception exception)
    {
        return exception is TelegramRequestException requestException
            ? requestException.HttpStatusCode
            : null;
    }

    private readonly record struct TelegramRequestExecutionContext(
        string MethodName,
        bool IsPollingRequest)
    {
        public static TelegramRequestExecutionContext Create(string methodName)
        {
            return new TelegramRequestExecutionContext(
                methodName,
                IsPollingMethod(methodName));
        }
    }

    private readonly struct TelegramRequestAttemptDiagnostics
    {
        private readonly TelegramRequestExecutor _executor;
        private readonly TelegramRequestExecutionContext _context;
        private readonly int _attempt;
        private readonly bool _debugEnabled;
        private readonly bool _errorEnabled;
        private readonly bool _timingEnabled;
        private readonly bool _enabled;
        private readonly long _started;

        private TelegramRequestAttemptDiagnostics(
            TelegramRequestExecutor executor,
            TelegramRequestExecutionContext context,
            int attempt,
            bool debugEnabled,
            bool errorEnabled,
            bool timingEnabled)
        {
            _executor = executor;
            _context = context;
            _attempt = attempt;
            _debugEnabled = debugEnabled;
            _errorEnabled = errorEnabled;
            _timingEnabled = timingEnabled;
            _enabled = debugEnabled || errorEnabled || timingEnabled;
            _started = _enabled ? executor._timeProvider.GetTimestamp() : 0;
        }

        public static TelegramRequestAttemptDiagnostics Create(
            TelegramRequestExecutor executor,
            TelegramRequestExecutionContext context,
            int attempt)
        {
            var debugEnabled = !context.IsPollingRequest && executor._logger.IsEnabled(LogLevel.Debug);
            var errorEnabled = !context.IsPollingRequest && executor._logger.IsEnabled(LogLevel.Error);
            var timingEnabled = !context.IsPollingRequest && TelegramHandlerRequestTimingScope.HasCurrent;

            return new TelegramRequestAttemptDiagnostics(
                executor,
                context,
                attempt,
                debugEnabled,
                errorEnabled,
                timingEnabled);
        }

        public void LogStarted(TelegramTransportContent content)
        {
            if (!_debugEnabled)
            {
                return;
            }

            LogRequestStarted(
                _executor._logger,
                _context.MethodName,
                _attempt,
                GetContentKind(content));
        }

        public void LogCompleted(int httpStatusCode)
        {
            if (!_enabled)
            {
                return;
            }

            var ended = _executor._timeProvider.GetTimestamp();
            RecordTiming(ended);

            if (!_debugEnabled)
            {
                return;
            }

            LogRequestCompleted(
                _executor._logger,
                _context.MethodName,
                _attempt,
                httpStatusCode,
                _executor.GetElapsedMilliseconds(_started, ended));
        }

        public void LogAttemptFailure(
            int? httpStatusCode,
            Exception exception)
        {
            if (!_enabled)
            {
                return;
            }

            var ended = _executor._timeProvider.GetTimestamp();
            RecordTiming(ended);

            if (_errorEnabled)
            {
                _executor.LogRequestFailure(
                    _context.MethodName,
                    _attempt,
                    httpStatusCode,
                    _started,
                    ended,
                    exception);
            }
        }

        public void LogFinalFailure(
            int httpStatusCode,
            Exception exception)
        {
            if (!_errorEnabled)
            {
                return;
            }

            _executor.LogRequestFailure(
                _context.MethodName,
                _attempt,
                httpStatusCode,
                _started,
                _executor._timeProvider.GetTimestamp(),
                exception);
        }

        public void LogThrottled(TelegramRetryAfterDelay retryAfter)
        {
            if (_context.IsPollingRequest ||
                !_executor._logger.IsEnabled(LogLevel.Warning))
            {
                return;
            }

            _executor.LogRequestThrottled(
                _context.MethodName,
                _attempt,
                retryAfter);
        }

        private void RecordTiming(long ended)
        {
            if (_timingEnabled)
            {
                TelegramHandlerRequestTimingScope.Record(_started, ended);
            }
        }
    }

    private static bool IsPollingMethod(string methodName)
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
