namespace TeleFlow.Annotations;

/// <summary>
/// Restricts a handler or handler class to updates sent by a non-bot Telegram user.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class FromHumanAttribute : TeleFlowAttribute;
