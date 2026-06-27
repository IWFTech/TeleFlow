namespace TeleFlow.Annotations;
/// <summary>
/// Requires the incoming message to reply to a message sent by the bot.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ReplyToBotAttribute : TeleFlowAttribute;
