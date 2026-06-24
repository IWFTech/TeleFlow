namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ChatTypeAttribute : TeleFlowAttribute
{
    public ChatTypeAttribute(params TelegramChatType[] chatTypes)
    {
        ArgumentNullException.ThrowIfNull(chatTypes);

        if (chatTypes.Length == 0)
        {
            throw new ArgumentException("At least one Telegram chat type must be specified.", nameof(chatTypes));
        }

        ChatTypes = chatTypes.ToArray();
    }

    public IReadOnlyList<TelegramChatType> ChatTypes { get; }
}
