using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.I18n;

/// <summary>
/// Provides immutable Telegram identity data to application locale resolvers before handler dispatch.
/// It deliberately excludes mutable framework state and localization-engine details.
/// </summary>
public sealed class LocaleResolutionContext
{
    public LocaleResolutionContext(Update update, User? user, Chat? chat)
    {
        Update = update ?? throw new ArgumentNullException(nameof(update));
        User = user;
        Chat = chat;
    }

    /// <summary>
    /// Gets the raw Telegram update being processed.
    /// </summary>
    public Update Update { get; }

    /// <summary>
    /// Gets the user who caused the update when Telegram provides one.
    /// </summary>
    public User? User { get; }

    /// <summary>
    /// Gets the chat associated with the update when Telegram provides one.
    /// </summary>
    public Chat? Chat { get; }
}
