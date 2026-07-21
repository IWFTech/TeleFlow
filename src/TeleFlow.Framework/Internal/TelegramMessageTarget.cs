using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

internal readonly record struct TelegramMessageTarget(
    Chat Chat,
    long MessageId,
    long? EphemeralMessageId,
    long? ReceiverUserId)
{
    public long ChatId => Chat.Id;

    public bool IsEphemeral => EphemeralMessageId is not null;
}
