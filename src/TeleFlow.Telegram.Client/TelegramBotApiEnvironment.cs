namespace TeleFlow.Telegram;

/// <summary>
/// Selects the Telegram Bot API environment used for every outgoing client request.
/// The test environment uses a separate Telegram account and bot token while preserving the normal client pipeline.
/// </summary>
public enum TelegramBotApiEnvironment
{
    /// <summary>
    /// Sends requests to the production Telegram Bot API endpoint.
    /// </summary>
    Production = 0,

    /// <summary>
    /// Sends requests to Telegram's dedicated Bot API test environment.
    /// </summary>
    Test = 1
}
