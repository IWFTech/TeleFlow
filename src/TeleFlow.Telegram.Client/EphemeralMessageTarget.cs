using TeleFlow.Telegram.Schema.Abstractions;

namespace TeleFlow.Telegram;

/// <summary>
/// Identifies the group or supergroup chat and recipient required to address an ephemeral Telegram message.
/// The target is used by client-level helpers outside an incoming TeleFlow update context.
/// </summary>
public sealed record EphemeralMessageTarget
{
    public EphemeralMessageTarget(IntegerString chatId, long receiverUserId)
    {
        ArgumentNullException.ThrowIfNull(chatId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(receiverUserId);

        ChatId = chatId;
        ReceiverUserId = receiverUserId;
    }

    public IntegerString ChatId { get; }

    public long ReceiverUserId { get; }
}
