namespace TeleFlow.Annotations;
/// <summary>
/// Requires the sender to have one of the configured Telegram member statuses in the chat.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireTelegramRoleAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a Telegram role requirement from a status set.
    /// </summary>
    /// <param name="allowedStatuses">Telegram member statuses allowed to invoke the handler.</param>
    public RequireTelegramRoleAttribute(TelegramMemberStatusSet allowedStatuses)
    {
        if (!TelegramMemberStatusSetValidator.IsValid(allowedStatuses))
        {
            throw new ArgumentException("Telegram role requirement must contain at least one known member status.", nameof(allowedStatuses));
        }

        AllowedStatuses = allowedStatuses;
    }

    /// <summary>
    /// Creates a Telegram role requirement from one or more status sets.
    /// </summary>
    /// <param name="allowedStatuses">Telegram member status sets allowed to invoke the handler.</param>
    public RequireTelegramRoleAttribute(params TelegramMemberStatusSet[] allowedStatuses)
    {
        ArgumentNullException.ThrowIfNull(allowedStatuses);

        if (allowedStatuses.Length == 0)
        {
            throw new ArgumentException("Telegram role requirement must contain at least one known member status.", nameof(allowedStatuses));
        }

        var combinedStatuses = TelegramMemberStatusSetValidator.Combine(allowedStatuses);

        if (!TelegramMemberStatusSetValidator.IsValid(combinedStatuses))
        {
            throw new ArgumentException("Telegram role requirement must contain only known member statuses.", nameof(allowedStatuses));
        }

        AllowedStatuses = combinedStatuses;
    }

    /// <summary>
    /// Telegram member statuses allowed to invoke the handler.
    /// </summary>
    public TelegramMemberStatusSet AllowedStatuses { get; }
}
