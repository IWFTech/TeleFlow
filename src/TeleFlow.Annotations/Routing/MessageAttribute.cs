namespace TeleFlow.Annotations;
/// <summary>
/// Marks a handler or handler class as matching Telegram message updates.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class MessageAttribute : TeleFlowAttribute
{
}
