namespace TeleFlow.Annotations;
/// <summary>
/// Restricts a handler or handler class to non-bot Telegram users, optionally limited by user id.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class FromUserAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a sender user filter.
    /// </summary>
    /// <param name="userIds">Optional Telegram user ids allowed to match.</param>
    public FromUserAttribute(params long[] userIds)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Any(static userId => userId <= 0))
        {
            throw new ArgumentException("Telegram user ids must be positive.", nameof(userIds));
        }

        UserIds = userIds.ToArray();
    }

    /// <summary>
    /// Optional Telegram user ids allowed to match. An empty collection allows any non-bot user.
    /// </summary>
    public IReadOnlyList<long> UserIds { get; }
}
