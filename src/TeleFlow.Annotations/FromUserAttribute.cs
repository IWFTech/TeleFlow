namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class FromUserAttribute : TeleFlowAttribute
{
    public FromUserAttribute(params long[] userIds)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Length == 0)
        {
            throw new ArgumentException("At least one Telegram user id must be specified.", nameof(userIds));
        }

        if (userIds.Any(static userId => userId <= 0))
        {
            throw new ArgumentException("Telegram user ids must be positive.", nameof(userIds));
        }

        UserIds = userIds.ToArray();
    }

    public IReadOnlyList<long> UserIds { get; }
}
