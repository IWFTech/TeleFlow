namespace TeleFlow.Annotations;
/// <summary>
/// Restricts a handler or handler class to specific forum message thread ids.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class MessageThreadIdAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a message thread id filter.
    /// </summary>
    /// <param name="messageThreadIds">Telegram message thread ids allowed to match.</param>
    public MessageThreadIdAttribute(params long[] messageThreadIds)
    {
        ArgumentNullException.ThrowIfNull(messageThreadIds);

        if (messageThreadIds.Length == 0)
        {
            throw new ArgumentException("At least one Telegram message thread id must be specified.", nameof(messageThreadIds));
        }

        if (messageThreadIds.Any(static messageThreadId => messageThreadId <= 0))
        {
            throw new ArgumentException("Telegram message thread ids must be positive.", nameof(messageThreadIds));
        }

        MessageThreadIds = messageThreadIds.ToArray();
    }

    /// <summary>
    /// Telegram message thread ids allowed to match.
    /// </summary>
    public IReadOnlyList<long> MessageThreadIds { get; }
}
