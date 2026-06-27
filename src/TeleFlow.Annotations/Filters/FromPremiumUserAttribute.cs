namespace TeleFlow.Annotations;
/// <summary>
/// Restricts a handler or handler class to updates sent by Telegram Premium users.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class FromPremiumUserAttribute : TeleFlowAttribute;
