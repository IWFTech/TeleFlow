namespace TeleFlow.Annotations;
/// <summary>
/// Requires the incoming message to belong to a forum message thread.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HasMessageThreadAttribute : TeleFlowAttribute;
