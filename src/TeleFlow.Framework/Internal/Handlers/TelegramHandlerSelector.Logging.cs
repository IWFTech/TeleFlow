using Microsoft.Extensions.Logging;

namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed partial class TelegramHandlerSelector
{
    /// <summary>
    /// Stable event ids emitted by the Telegram handler selector.
    /// </summary>
    private static class LogEventIds
    {
        /// <summary>
        /// A typed callback route matched callback data shape, but payload decoding failed.
        /// </summary>
        public const int CallbackDataDeserializationFailed = 1;

        /// <summary>
        /// A route matched the incoming update, but filters rejected the handler candidate.
        /// </summary>
        public const int HandlerRejectedByFilters = 2;
    }

    /// <summary>
    /// Logs malformed or stale typed callback data without exposing the raw callback payload.
    /// </summary>
    [LoggerMessage(
        EventId = LogEventIds.CallbackDataDeserializationFailed,
        Level = LogLevel.Warning,
        Message = "Telegram callback data failed to deserialize. update_id={UpdateId}, payload_type={PayloadType}, handler={Handler}, route={Route}, callback_data_bytes={CallbackDataBytes}. Treating callback route as not matched.")]
    private static partial void LogCallbackDataDeserializationFailed(
        ILogger logger,
        Exception exception,
        long updateId,
        string payloadType,
        string handler,
        string route,
        int callbackDataBytes);

    /// <summary>
    /// Logs route candidates rejected by filters without exposing user message text or callback data.
    /// </summary>
    [LoggerMessage(
        EventId = LogEventIds.HandlerRejectedByFilters,
        Level = LogLevel.Debug,
        Message = "Telegram handler candidate rejected by filters. update_id={UpdateId}, type={UpdateType}, handler={Handler}, route={Route}.")]
    private static partial void LogHandlerRejectedByFilters(
        ILogger logger,
        long updateId,
        string updateType,
        string handler,
        string route);
}
