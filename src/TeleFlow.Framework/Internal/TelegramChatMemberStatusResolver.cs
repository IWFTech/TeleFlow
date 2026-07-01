using TeleFlow.Annotations;
using TeleFlow.Telegram.Schema.Abstractions;

namespace TeleFlow.Telegram.Internal;

internal sealed class TelegramChatMemberStatusResolver : ITelegramChatMemberStatusResolver
{
    public async ValueTask<TelegramMemberStatusSet?> ResolveAsync(
        TelegramUpdateContext context,
        long chatId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var chatMember = await context.Bot.GetChatMemberAsync(
            IntegerString.From(chatId),
            userId,
            cancellationToken).ConfigureAwait(false);

        return TelegramChatMemberClassifier.GetStatus(chatMember);
    }
}
