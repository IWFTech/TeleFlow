namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class TextTemplateAttribute : TeleFlowAttribute
{
    public TextTemplateAttribute(string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        Template = template;
    }

    public string Template { get; }

    public bool IgnoreCase { get; set; } = true;
}
