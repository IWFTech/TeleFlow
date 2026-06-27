namespace TeleFlow.Annotations;
/// <summary>
/// Routes a chat member update when old and new member statuses match the configured sets.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ChatMemberChangedAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a chat member status change route.
    /// </summary>
    /// <param name="oldStatus">Allowed previous member statuses.</param>
    /// <param name="newStatus">Allowed new member statuses.</param>
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

    /// <summary>
    /// Allowed previous member statuses.
    /// </summary>
    public TelegramMemberStatusSet OldStatus { get; }

    /// <summary>
    /// Allowed new member statuses.
    /// </summary>
    public TelegramMemberStatusSet NewStatus { get; }
}
