namespace TeleFlow.Annotations;
/// <summary>
/// Requires the incoming message to contain an animation.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HasAnimationAttribute : TeleFlowAttribute;
