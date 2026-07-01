using TeleFlow.Framework.States;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

/// <summary>
/// Provides scoped read-only access to the Telegram update currently being processed by TeleFlow.
/// Application services can use it to read current user, chat, message, callback, and state identity
/// without accepting handler contexts directly.
/// </summary>
public interface ITelegramCurrentUpdateAccessor
{
    /// <summary>
    /// Gets whether the active DI scope is currently processing a Telegram update.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets the current Telegram update context or throws outside Telegram update processing.
    /// </summary>
    TelegramUpdateContext Current { get; }

    /// <summary>
    /// Gets the current raw Telegram update or throws outside Telegram update processing.
    /// </summary>
    Update Update { get; }

    /// <summary>
    /// Gets the user who caused the current update when Telegram provides one.
    /// </summary>
    User? User { get; }

    /// <summary>
    /// Gets the chat associated with the current update when Telegram provides one.
    /// </summary>
    Chat? Chat { get; }

    /// <summary>
    /// Gets the current message when the update is a message update.
    /// </summary>
    Message? Message { get; }

    /// <summary>
    /// Gets the current callback query when the update is a callback query update.
    /// </summary>
    CallbackQuery? CallbackQuery { get; }

    /// <summary>
    /// Gets the current chat member update when the update changed a member status.
    /// </summary>
    ChatMemberUpdated? ChatMemberUpdated { get; }

    /// <summary>
    /// Gets the state key produced by state middleware for the current update when available.
    /// </summary>
    StateKey? StateKey { get; }

    /// <summary>
    /// Attempts to get the current Telegram update context without throwing outside update processing.
    /// </summary>
    bool TryGetCurrent(out TelegramUpdateContext context);

    /// <summary>
    /// Attempts to get the current message context when the update is a message update.
    /// </summary>
    bool TryGetMessageContext(out MessageContext context);

    /// <summary>
    /// Attempts to get the current callback query context when the update is a callback query update.
    /// </summary>
    bool TryGetCallbackQueryContext(out CallbackQueryContext context);

    /// <summary>
    /// Attempts to get the current chat member context when the update changed a member status.
    /// </summary>
    bool TryGetChatMemberUpdatedContext(out ChatMemberUpdatedContext context);
}
