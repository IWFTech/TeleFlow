namespace TeleFlow.Annotations;
/// <summary>
/// Requires the incoming callback query to contain callback data.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HasCallbackDataAttribute : TeleFlowAttribute;
