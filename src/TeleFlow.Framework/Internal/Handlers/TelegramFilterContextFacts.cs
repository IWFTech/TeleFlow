using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal.Handlers;

/// <summary>
/// Captures destination, sender, and thread facts once before built-in filters evaluate an update.
/// It keeps Telegram's destination chat, actual sender user, and <c>sender_chat</c> provenance as separate concepts.
/// </summary>
internal readonly record struct TelegramFilterContextFacts(
    Chat? DestinationChat,
    User? SenderUser,
    Chat? SenderChat,
    long? MessageThreadId)
{
    public static TelegramFilterContextFacts From(TelegramUpdateContext context)
    {
        return context switch
        {
            MessageContext messageContext => FromMessage(messageContext.TelegramMessage),

            CallbackQueryContext callbackContext => FromCallback(callbackContext),

            ChatMemberUpdatedContext chatMemberContext => new TelegramFilterContextFacts(
                chatMemberContext.TelegramChat,
                SenderUser: null,
                SenderChat: null,
                MessageThreadId: null),

            _ => default
        };
    }

    private static TelegramFilterContextFacts FromMessage(Message message)
    {
        var senderChat = message.SenderChat;

        return new TelegramFilterContextFacts(
            message.Chat,
            senderChat is null ? message.From : null,
            senderChat,
            message.MessageThreadId);
    }

    private static TelegramFilterContextFacts FromCallback(CallbackQueryContext context)
    {
        if (context.TelegramCallbackQuery.Message is not { } maybeMessage)
        {
            return new TelegramFilterContextFacts(
                DestinationChat: null,
                context.TelegramCallbackQuery.From,
                SenderChat: null,
                MessageThreadId: null);
        }

        if (maybeMessage.TryGetMessage(out var message) &&
            message is not null)
        {
            return new TelegramFilterContextFacts(
                message.Chat,
                context.TelegramCallbackQuery.From,
                SenderChat: null,
                message.MessageThreadId);
        }

        return new TelegramFilterContextFacts(
            DestinationChat: null,
            context.TelegramCallbackQuery.From,
            SenderChat: null,
            MessageThreadId: null);
    }
}
