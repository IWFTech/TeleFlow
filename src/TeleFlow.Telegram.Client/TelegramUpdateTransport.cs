namespace TeleFlow.Telegram;

/// <summary>
/// Identifies the Telegram update transport that encountered an update payload it could not decode.
/// </summary>
public enum TelegramUpdateTransport
{
    LongPolling,
    Webhook
}
