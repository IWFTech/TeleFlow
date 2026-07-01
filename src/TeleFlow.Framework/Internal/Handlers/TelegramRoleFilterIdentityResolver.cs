namespace TeleFlow.Telegram.Internal.Handlers;

internal static class TelegramRoleFilterIdentityResolver
{
    public static bool TryResolve(
        TelegramUpdateContext context,
        out TelegramRoleFilterIdentity identity)
    {
        switch (context)
        {
            case MessageContext messageContext:
                if (messageContext.TelegramMessage.From is null)
                {
                    identity = default;
                    return false;
                }

                identity = new TelegramRoleFilterIdentity(
                    messageContext.TelegramChat.Id,
                    messageContext.TelegramMessage.From.Id);
                return true;

            case CallbackQueryContext callbackContext:
                if (callbackContext.TelegramCallbackQuery.Message is not { } maybeMessage ||
                    !maybeMessage.TryGetMessage(out var message) ||
                    message is null)
                {
                    identity = default;
                    return false;
                }

                identity = new TelegramRoleFilterIdentity(
                    message.Chat.Id,
                    callbackContext.TelegramCallbackQuery.From.Id);
                return true;

            case ChatMemberUpdatedContext chatMemberContext:
                identity = new TelegramRoleFilterIdentity(
                    chatMemberContext.TelegramChat.Id,
                    chatMemberContext.Actor.Id);
                return true;

            default:
                identity = default;
                return false;
        }
    }
}
