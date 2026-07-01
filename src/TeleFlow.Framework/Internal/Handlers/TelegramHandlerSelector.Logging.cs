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
}
