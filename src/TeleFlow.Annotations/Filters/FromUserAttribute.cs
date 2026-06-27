namespace TeleFlow.Annotations;
/// <summary>
/// Restricts a handler or handler class to specific Telegram user ids.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class FromUserAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a sender user id filter.
    /// </summary>
    /// <param name="userIds">Telegram user ids allowed to match.</param>
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

    /// <summary>
    /// Telegram user ids allowed to match.
    /// </summary>
    public IReadOnlyList<long> UserIds { get; }
}
