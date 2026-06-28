using Microsoft.Extensions.Logging;

namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed partial class TelegramHandlerDispatcher
{
    private static class LogEventIds
    {
        public const int NoHandlerMatched = 1;
        public const int HandlerMatched = 2;
        public const int HandlerFailed = 3;
        public const int HandlerCompleted = 4;
        public const int ErrorHandlerCompleted = 5;
    }

    private readonly record struct HandlerFailureLogContext(
        long UpdateId,
        string UpdateType,
        string Handler,
        string Route,
        string ModuleName,
        string SceneName,
        string ExceptionType);

    private void LogHandlerFailure(
        Exception exception,
        HandlerFailureLogContext context,
        bool includeTiming,
        long handlerStarted,
        TelegramHandlerRequestTimingScope? requestTimingScope)
    {
        if (!includeTiming)
        {
            LogHandlerFailed(
                _logger,
                exception,
                context.UpdateId,
                context.UpdateType,
                context.Handler,
                context.Route,
                context.ModuleName,
                context.SceneName,
                context.ExceptionType);
            return;
        }

        ArgumentNullException.ThrowIfNull(requestTimingScope);

        var handlerElapsed = _timeProvider.GetElapsedTime(handlerStarted);
        var timing = requestTimingScope.CreateSummary(_timeProvider, handlerElapsed);

        LogHandlerFailedWithTiming(
            _logger,
            exception,
            context.UpdateId,
            context.UpdateType,
            context.Handler,
            context.Route,
            context.ModuleName,
            context.SceneName,
            context.ExceptionType,
            handlerElapsed.TotalMilliseconds,
            timing.RequestCount,
            timing.RequestElapsedMilliseconds,
            timing.HandlerLogicElapsedMilliseconds);
    }

    [LoggerMessage(
        EventId = LogEventIds.NoHandlerMatched,
        Level = LogLevel.Debug,
        Message = "No Telegram handler matched. update_id={UpdateId}, type={UpdateType}, match_ms={MatchElapsedMilliseconds:F2}.")]
    private static partial void LogNoHandlerMatched(
        ILogger logger,
        long updateId,
        string updateType,
        double matchElapsedMilliseconds);

    [LoggerMessage(
        EventId = LogEventIds.HandlerMatched,
        Level = LogLevel.Debug,
        Message = "Telegram handler matched. update_id={UpdateId}, type={UpdateType}, handler={Handler}, route={Route}, module={ModuleName}, scene={SceneName}, match_ms={MatchElapsedMilliseconds:F2}.")]
    private static partial void LogHandlerMatched(
        ILogger logger,
        long updateId,
        string updateType,
        string handler,
        string route,
        string moduleName,
        string sceneName,
        double matchElapsedMilliseconds);

    [LoggerMessage(
        EventId = LogEventIds.HandlerFailed,
        Level = LogLevel.Error,
        Message = "Telegram handler failed. update_id={UpdateId}, type={UpdateType}, handler={Handler}, route={Route}, module={ModuleName}, scene={SceneName}, exception_type={ExceptionType}.")]
    private static partial void LogHandlerFailed(
        ILogger logger,
        Exception exception,
        long updateId,
        string updateType,
        string handler,
        string route,
        string moduleName,
        string sceneName,
        string exceptionType);

    [LoggerMessage(
        EventId = LogEventIds.HandlerFailed,
        Level = LogLevel.Error,
        Message = "Telegram handler failed. update_id={UpdateId}, type={UpdateType}, handler={Handler}, route={Route}, module={ModuleName}, scene={SceneName}, exception_type={ExceptionType}, handler_ms={HandlerElapsedMilliseconds:F2}, telegram_request_count={TelegramRequestCount}, telegram_request_ms={TelegramRequestElapsedMilliseconds:F2}, handler_logic_ms={HandlerLogicElapsedMilliseconds:F2}.")]
    private static partial void LogHandlerFailedWithTiming(
        ILogger logger,
        Exception exception,
        long updateId,
        string updateType,
        string handler,
        string route,
        string moduleName,
        string sceneName,
        string exceptionType,
        double handlerElapsedMilliseconds,
        int telegramRequestCount,
        double telegramRequestElapsedMilliseconds,
        double handlerLogicElapsedMilliseconds);

    [LoggerMessage(
        EventId = LogEventIds.HandlerCompleted,
        Level = LogLevel.Debug,
        Message = "Telegram handler completed. update_id={UpdateId}, type={UpdateType}, handler={Handler}, route={Route}, handler_ms={HandlerElapsedMilliseconds:F2}, telegram_request_count={TelegramRequestCount}, telegram_request_ms={TelegramRequestElapsedMilliseconds:F2}, handler_logic_ms={HandlerLogicElapsedMilliseconds:F2}.")]
    private static partial void LogHandlerCompleted(
        ILogger logger,
        long updateId,
        string updateType,
        string handler,
        string route,
        double handlerElapsedMilliseconds,
        int telegramRequestCount,
        double telegramRequestElapsedMilliseconds,
        double handlerLogicElapsedMilliseconds);

    [LoggerMessage(
        EventId = LogEventIds.ErrorHandlerCompleted,
        Level = LogLevel.Debug,
        Message = "Telegram error handler completed. update_id={UpdateId}, type={UpdateType}, handler={Handler}, route={Route}, module={ModuleName}, scene={SceneName}, exception_type={ExceptionType}, error_handler={ErrorHandler}, handled={Handled}.")]
    private static partial void LogErrorHandlerCompleted(
        ILogger logger,
        long updateId,
        string updateType,
        string handler,
        string route,
        string moduleName,
        string sceneName,
        string exceptionType,
        string errorHandler,
        bool handled);
}
