namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class FromBotAttribute(bool value = true) : TeleFlowAttribute
{
    public bool Value { get; } = value;
}
