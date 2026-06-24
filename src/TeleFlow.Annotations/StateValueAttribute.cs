namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class StateValueAttribute : TeleFlowAttribute
{
    public StateValueAttribute(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
}
