namespace TeleFlow.Annotations;

/// <summary>
/// Telegram chat types that can be used by chat-type filters.
/// </summary>
public enum TelegramChatType
{
    /// <summary>
    /// One-to-one private chat with a user.
    /// </summary>
    Private,

    /// <summary>
    /// Basic Telegram group chat.
    /// </summary>
    Group,

    /// <summary>
    /// Telegram supergroup chat.
    /// </summary>
    Supergroup,

    /// <summary>
    /// Telegram channel chat.
    /// </summary>
    Channel,
}
