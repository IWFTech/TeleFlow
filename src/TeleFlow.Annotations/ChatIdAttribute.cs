namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ChatIdAttribute : TeleFlowAttribute
{
    public ChatIdAttribute(params long[] chatIds)
    {
        ArgumentNullException.ThrowIfNull(chatIds);

        if (chatIds.Length == 0)
        {
            throw new ArgumentException("At least one Telegram chat id must be specified.", nameof(chatIds));
        }

        if (chatIds.Any(static chatId => chatId == 0))
        {
            throw new ArgumentException("Telegram chat ids must not be zero.", nameof(chatIds));
        }

        ChatIds = chatIds.ToArray();
    }

    public IReadOnlyList<long> ChatIds { get; }
}
