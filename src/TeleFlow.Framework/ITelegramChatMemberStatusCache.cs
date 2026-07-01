using TeleFlow.Annotations;

namespace TeleFlow.Telegram;

public interface ITelegramChatMemberStatusCache
{
    ValueTask<TelegramMemberStatusSet?> GetAsync(
        long chatId,
        long userId,
        CancellationToken cancellationToken = default);

    ValueTask SetAsync(
        long chatId,
        long userId,
        TelegramMemberStatusSet status,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);
}
