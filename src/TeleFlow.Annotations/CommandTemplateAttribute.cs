namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class CommandTemplateAttribute : TeleFlowAttribute
{
    public CommandTemplateAttribute(string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        Template = template;
    }

    public string Template { get; }

    public string[] Prefixes { get; set; } = ["/"];

    public bool AllowSpaceAfterPrefix { get; set; }

    public bool IgnoreCase { get; set; } = true;
}
