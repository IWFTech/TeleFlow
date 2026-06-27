namespace TeleFlow.Annotations;
/// <summary>
/// Routes a Telegram text message by matching a template and binding template values.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class TextTemplateAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a text template route.
    /// </summary>
    /// <param name="template">Text template such as <c>order {id:long}</c>.</param>
    public TextTemplateAttribute(string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        Template = template;
    }

    /// <summary>
    /// Text template used for route matching and value binding.
    /// </summary>
    public string Template { get; }

    /// <summary>
    /// Matches literal template parts without case sensitivity.
    /// </summary>
    public bool IgnoreCase { get; set; } = true;
}
