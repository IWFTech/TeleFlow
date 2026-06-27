namespace TeleFlow.Annotations;
/// <summary>
/// Requires the incoming message to contain audio.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HasAudioAttribute : TeleFlowAttribute;
