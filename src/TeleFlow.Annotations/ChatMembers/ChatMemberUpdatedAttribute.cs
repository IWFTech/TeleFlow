namespace TeleFlow.Annotations;
/// <summary>
/// Marks a handler or handler class as matching Telegram <c>chat_member</c> updates.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ChatMemberUpdatedAttribute : TeleFlowAttribute;
