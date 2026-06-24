namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class CommandRegexAttribute : TeleFlowAttribute
{
    public CommandRegexAttribute(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        Pattern = pattern;
    }

    public string Pattern { get; }

    public string[] Prefixes { get; set; } = ["/"];

    public bool AllowSpaceAfterPrefix { get; set; }

    public bool IgnoreCase { get; set; } = true;
}
