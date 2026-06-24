namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class TextAttribute : TeleFlowAttribute
{
    public TextAttribute(
        string value,
        TextMatchMode mode = TextMatchMode.Equals,
        bool ignoreCase = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
        Mode = mode;
        IgnoreCase = ignoreCase;
    }

    public string Value { get; }

    public TextMatchMode Mode { get; }

    public bool IgnoreCase { get; }
}
