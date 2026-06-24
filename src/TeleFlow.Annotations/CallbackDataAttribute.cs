namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class CallbackDataAttribute : TeleFlowAttribute
{
    public CallbackDataAttribute(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        Prefix = prefix;
    }

    public string Prefix { get; }
}
