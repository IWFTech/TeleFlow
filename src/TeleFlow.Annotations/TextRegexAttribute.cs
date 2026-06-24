namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class TextRegexAttribute : TeleFlowAttribute
{
    public TextRegexAttribute(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        Pattern = pattern;
    }

    public string Pattern { get; }

    public bool IgnoreCase { get; set; } = true;
}
