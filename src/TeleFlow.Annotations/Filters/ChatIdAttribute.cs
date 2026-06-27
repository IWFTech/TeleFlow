namespace TeleFlow.Annotations;
/// <summary>
/// Restricts a handler or handler class to specific Telegram chat ids.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ChatIdAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a chat id filter.
    /// </summary>
    /// <param name="chatIds">Telegram chat ids allowed to match.</param>
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

    /// <summary>
    /// Telegram chat ids allowed to match.
    /// </summary>
    public IReadOnlyList<long> ChatIds { get; }
}
