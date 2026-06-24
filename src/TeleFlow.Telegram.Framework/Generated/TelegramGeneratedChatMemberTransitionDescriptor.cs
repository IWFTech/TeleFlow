using System.ComponentModel;
using TeleFlow.Annotations;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure chat-member transition metadata emitted by TeleFlow source generators.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TelegramGeneratedChatMemberTransitionDescriptor
{
    public TelegramGeneratedChatMemberTransitionDescriptor(
        TelegramMemberStatusSet oldStatus,
        TelegramMemberStatusSet newStatus)
    {
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }

    public TelegramMemberStatusSet OldStatus { get; }

    public TelegramMemberStatusSet NewStatus { get; }
}
