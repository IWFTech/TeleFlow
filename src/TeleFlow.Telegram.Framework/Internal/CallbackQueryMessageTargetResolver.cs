using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

internal static class CallbackQueryMessageTargetResolver
{
    public static bool TryResolve(CallbackQuery callbackQuery, out TelegramMessageTarget target)
    {
        ArgumentNullException.ThrowIfNull(callbackQuery);

        var message = callbackQuery.Message;
        if (message is null)
        {
            target = default;
            return false;
        }

        if (message.TryGetMessage(out var accessibleMessage) && accessibleMessage is not null)
        {
            target = new TelegramMessageTarget(accessibleMessage.Chat.Id, accessibleMessage.MessageId);
            return true;
        }

        if (message.TryGetInaccessibleMessage(out var inaccessibleMessage) && inaccessibleMessage is not null)
        {
            target = new TelegramMessageTarget(inaccessibleMessage.Chat.Id, inaccessibleMessage.MessageId);
            return true;
        }

        target = default;
        return false;
    }
}
