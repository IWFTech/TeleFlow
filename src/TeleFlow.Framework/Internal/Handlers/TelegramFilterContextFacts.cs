using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal.Handlers;

internal readonly record struct TelegramFilterContextFacts(
    Chat? Chat,
    long? MessageThreadId)
{
    public static TelegramFilterContextFacts From(TelegramUpdateContext context)
    {
        return context switch
        {
            MessageContext messageContext => new TelegramFilterContextFacts(
                messageContext.TelegramMessage.Chat,
                messageContext.TelegramMessage.MessageThreadId),

            CallbackQueryContext callbackContext => FromCallback(callbackContext),

            ChatMemberUpdatedContext chatMemberContext => new TelegramFilterContextFacts(
                chatMemberContext.TelegramChat,
                MessageThreadId: null),

            _ => default
        };
    }

    private static TelegramFilterContextFacts FromCallback(CallbackQueryContext context)
    {
        if (context.TelegramCallbackQuery.Message is not { } maybeMessage)
        {
            return default;
        }

        if (maybeMessage.TryGetMessage(out var message) &&
            message is not null)
        {
            return new TelegramFilterContextFacts(
                message.Chat,
                message.MessageThreadId);
        }

        return default;
    }
}
