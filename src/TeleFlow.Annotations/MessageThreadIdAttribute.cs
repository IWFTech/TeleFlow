namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class MessageThreadIdAttribute : TeleFlowAttribute
{
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

    public IReadOnlyList<long> MessageThreadIds { get; }
}
