namespace TeleFlow.Annotations;
/// <summary>
/// Restricts a handler or handler class to specific Telegram chat types.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ChatTypeAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a chat type filter.
    /// </summary>
    /// <param name="chatTypes">Telegram chat types allowed to match.</param>
    public ChatTypeAttribute(params TelegramChatType[] chatTypes)
    {
        ArgumentNullException.ThrowIfNull(chatTypes);

        if (chatTypes.Length == 0)
        {
            throw new ArgumentException("At least one Telegram chat type must be specified.", nameof(chatTypes));
        }

        ChatTypes = chatTypes.ToArray();
    }

    /// <summary>
    /// Telegram chat types allowed to match.
    /// </summary>
    public IReadOnlyList<TelegramChatType> ChatTypes { get; }
}
