using TeleFlow.Annotations;

namespace TeleFlow.Telegram;

public interface ITelegramChatMemberStatusResolver
{
    ValueTask<TelegramMemberStatusSet?> ResolveAsync(
        TelegramUpdateContext context,
        long chatId,
        long userId,
        CancellationToken cancellationToken = default);
}
