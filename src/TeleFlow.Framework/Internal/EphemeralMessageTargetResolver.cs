using TeleFlow.Telegram.Schema.Constants;
using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Derives the Bot API target for ephemeral actions from message and callback updates.
/// It keeps Telegram's group-only recipient rules out of the public action classes.
/// </summary>
internal static class EphemeralMessageTargetResolver
{
    public static EphemeralMessageTarget ResolveForMessage(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return CreateTarget(
            message.Chat,
            message.ReceiverUser?.Id ?? message.From?.Id,
            "The current message does not identify a user who can receive an ephemeral message.");
    }

    public static EphemeralMessageTarget ResolveForCallback(
        CallbackQuery callbackQuery,
        TelegramMessageTarget messageTarget)
    {
        ArgumentNullException.ThrowIfNull(callbackQuery);

        return CreateTarget(
            messageTarget.Chat,
            callbackQuery.From.Id,
            "The current callback query does not identify a user who can receive an ephemeral message.");
    }

    public static EphemeralMessageTarget ResolveForEphemeralMessage(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return CreateTarget(
            message.Chat,
            message.ReceiverUser?.Id,
            "The current ephemeral message does not identify its receiver.");
    }

    public static long ResolveReceiverUserId(
        CallbackQuery callbackQuery,
        TelegramMessageTarget messageTarget)
    {
        ArgumentNullException.ThrowIfNull(callbackQuery);

        return messageTarget.ReceiverUserId ?? callbackQuery.From.Id;
    }

    private static EphemeralMessageTarget CreateTarget(
        Chat chat,
        long? receiverUserId,
        string missingReceiverMessage)
    {
        ArgumentNullException.ThrowIfNull(chat);

        if (!string.Equals(chat.Type, ChatTypes.Group, StringComparison.Ordinal) &&
            !string.Equals(chat.Type, ChatTypes.Supergroup, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Ephemeral messages can only be sent in a group or supergroup chat.");
        }

        if (receiverUserId is not long userId || userId <= 0)
        {
            throw new InvalidOperationException(missingReceiverMessage);
        }

        return new EphemeralMessageTarget(IntegerString.From(chat.Id), userId);
    }
}
