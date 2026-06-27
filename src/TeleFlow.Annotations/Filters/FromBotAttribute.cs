namespace TeleFlow.Annotations;
/// <summary>
/// Restricts a handler or handler class by whether the sender is a bot.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class FromBotAttribute(bool value = true) : TeleFlowAttribute
{
    /// <summary>
    /// Required sender bot flag value.
    /// </summary>
    public bool Value { get; } = value;
}
