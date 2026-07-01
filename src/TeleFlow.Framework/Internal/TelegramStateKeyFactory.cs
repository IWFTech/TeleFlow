using TeleFlow.Framework.States;
using TeleFlow.Framework.Updates;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

internal sealed class TelegramStateKeyFactory : IStateKeyFactory
{
    public bool TryCreateStateKey(UpdateContext context, out StateKey key)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Payload is not TelegramUpdatePayload payload)
        {
            key = default;
            return false;
        }

        if (TryCreateFromMessage(payload.Update.Message, out key) ||
            TryCreateFromCallback(payload.Update.CallbackQuery, out key))
        {
            return true;
        }

        key = default;
        return false;
    }

    private static bool TryCreateFromMessage(Message? message, out StateKey key)
    {
        if (message?.From is null)
        {
            key = default;
            return false;
        }

        key = StateKey.Create(
            scope: "telegram",
            subject: CreateUserSubject(message.From.Id),
            partition: CreateChatPartition(message.Chat.Id));
        return true;
    }

    private static bool TryCreateFromCallback(CallbackQuery? callbackQuery, out StateKey key)
    {
        if (callbackQuery is null)
        {
            key = default;
            return false;
        }

        key = StateKey.Create(
            scope: "telegram",
            subject: CreateUserSubject(callbackQuery.From.Id),
            partition: ResolveCallbackPartition(callbackQuery));
        return true;
    }

    private static string ResolveCallbackPartition(CallbackQuery callbackQuery)
    {
        if (callbackQuery.Message is not null)
        {
            if (callbackQuery.Message.TryGetMessage(out var message) && message is not null)
            {
                return CreateChatPartition(message.Chat.Id);
            }

            if (callbackQuery.Message.TryGetInaccessibleMessage(out var inaccessibleMessage) &&
                inaccessibleMessage is not null)
            {
                return CreateChatPartition(inaccessibleMessage.Chat.Id);
            }
        }

        return string.IsNullOrWhiteSpace(callbackQuery.ChatInstance)
            ? "inline:unknown"
            : $"inline:{callbackQuery.ChatInstance}";
    }

    private static string CreateUserSubject(long userId)
    {
        return $"user:{userId}";
    }

    private static string CreateChatPartition(long chatId)
    {
        return $"chat:{chatId}";
    }
}
