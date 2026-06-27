namespace TeleFlow.Annotations;
/// <summary>
/// Requires the incoming message to be a reply to another message.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class IsReplyAttribute : TeleFlowAttribute;
