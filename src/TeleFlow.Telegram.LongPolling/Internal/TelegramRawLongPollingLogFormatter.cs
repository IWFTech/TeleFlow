using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

internal static class TelegramRawLongPollingLogFormatter
{
    public static string FormatAllowedUpdates(IReadOnlyList<string>? allowedUpdates)
    {
        return allowedUpdates is null
            ? "unset"
            : string.Join(",", allowedUpdates);
    }

    public static string GetUpdateType(Update update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (update.Message is not null)
        {
            return "message";
        }

        if (update.CallbackQuery is not null)
        {
            return "callback_query";
        }

        if (update.MyChatMember is not null)
        {
            return "my_chat_member";
        }

        if (update.ChatMember is not null)
        {
            return "chat_member";
        }

        return "unknown";
    }
}
