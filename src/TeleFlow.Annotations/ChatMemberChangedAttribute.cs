namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ChatMemberChangedAttribute : TeleFlowAttribute
{
    public ChatMemberChangedAttribute(
        TelegramMemberStatusSet oldStatus,
        TelegramMemberStatusSet newStatus)
    {
        if (!TelegramMemberStatusSetValidator.IsValid(oldStatus))
        {
            throw new ArgumentException("Old Telegram member status set must contain at least one known status.", nameof(oldStatus));
        }

        if (!TelegramMemberStatusSetValidator.IsValid(newStatus))
        {
            throw new ArgumentException("New Telegram member status set must contain at least one known status.", nameof(newStatus));
        }

        OldStatus = oldStatus;
        NewStatus = newStatus;
    }

    public TelegramMemberStatusSet OldStatus { get; }

    public TelegramMemberStatusSet NewStatus { get; }
}
