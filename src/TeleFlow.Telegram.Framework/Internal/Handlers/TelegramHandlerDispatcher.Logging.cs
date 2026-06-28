using Microsoft.Extensions.Logging;

namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed partial class TelegramHandlerDispatcher
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "No Telegram handler matched. update_id={UpdateId}, type={UpdateType}, match_ms={MatchElapsedMilliseconds:F2}.")]
    private static partial void LogNoHandlerMatched(
        ILogger logger,
        long updateId,
        string updateType,
        double matchElapsedMilliseconds);

    [LoggerMessage(
        EventId = 2,
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
        EventId = 3,
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
        EventId = 3,
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
        EventId = 4,
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
        EventId = 5,
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
