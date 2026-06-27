namespace TeleFlow.Annotations;
/// <summary>
/// Requires the incoming message to contain text.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HasTextAttribute : TeleFlowAttribute;
