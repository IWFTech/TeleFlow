namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireTelegramRoleAttribute : TeleFlowAttribute
{
    public RequireTelegramRoleAttribute(TelegramMemberStatusSet allowedStatuses)
    {
        if (!TelegramMemberStatusSetValidator.IsValid(allowedStatuses))
        {
            throw new ArgumentException("Telegram role requirement must contain at least one known member status.", nameof(allowedStatuses));
        }

        AllowedStatuses = allowedStatuses;
    }

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

    public TelegramMemberStatusSet AllowedStatuses { get; }
}
