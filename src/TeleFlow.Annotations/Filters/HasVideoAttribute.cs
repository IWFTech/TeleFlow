namespace TeleFlow.Annotations;
/// <summary>
/// Requires the incoming message to contain a video.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HasVideoAttribute : TeleFlowAttribute;
