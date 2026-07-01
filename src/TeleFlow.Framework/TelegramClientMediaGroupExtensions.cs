using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public static class TelegramClientMediaGroupExtensions
{
    public static Task<IReadOnlyList<Message>> SendMediaGroupAsync(
        this ITelegramClient bot,
        IntegerString chatId,
        MediaGroup media,
        string? businessConnectionId = null,
        long? messageThreadId = null,
        long? directMessagesTopicId = null,
        bool? disableNotification = null,
        bool? protectContent = null,
        bool? allowPaidBroadcast = null,
        string? messageEffectId = null,
        ReplyParameters? replyParameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(media);

        return bot.SendMediaGroupAsync(
            chatId,
            media.ToMedia(),
            businessConnectionId,
            messageThreadId,
            directMessagesTopicId,
            disableNotification,
            protectContent,
            allowPaidBroadcast,
            messageEffectId,
            replyParameters,
            cancellationToken);
    }
}
