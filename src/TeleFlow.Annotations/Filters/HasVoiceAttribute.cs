namespace TeleFlow.Annotations;
/// <summary>
/// Requires the incoming message to contain a voice message.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HasVoiceAttribute : TeleFlowAttribute;
