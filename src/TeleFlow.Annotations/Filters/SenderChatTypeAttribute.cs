namespace TeleFlow.Annotations;

/// <summary>
/// Restricts a message or command handler to messages sent on behalf of specific Telegram chat types.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class SenderChatTypeAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a sender chat type filter.
    /// </summary>
    /// <param name="chatTypes">Telegram chat types allowed for <c>message.sender_chat</c>.</param>
    public SenderChatTypeAttribute(params TelegramChatType[] chatTypes)
    {
        ArgumentNullException.ThrowIfNull(chatTypes);

        if (chatTypes.Length == 0)
        {
            throw new ArgumentException("At least one Telegram sender chat type must be specified.", nameof(chatTypes));
        }

        ChatTypes = chatTypes.ToArray();
    }

    /// <summary>
    /// Telegram chat types allowed for <c>message.sender_chat</c>.
    /// </summary>
    public IReadOnlyList<TelegramChatType> ChatTypes { get; }
}
