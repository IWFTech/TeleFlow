namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class StateGroupAttribute : TeleFlowAttribute
{
    public StateGroupAttribute(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        Prefix = prefix;
    }

    public string Prefix { get; }
}
