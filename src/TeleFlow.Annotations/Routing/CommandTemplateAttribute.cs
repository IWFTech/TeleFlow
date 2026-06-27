namespace TeleFlow.Annotations;
/// <summary>
/// Routes a Telegram command message by matching a template and binding template values.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class CommandTemplateAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a command template route.
    /// </summary>
    /// <param name="template">Command template such as <c>ticket {id:long?}</c>.</param>
    public CommandTemplateAttribute(string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        Template = template;
    }

    /// <summary>
    /// Command template used for route matching and value binding.
    /// </summary>
    public string Template { get; }

    /// <summary>
    /// Command prefixes accepted by this route. Defaults to <c>/</c>.
    /// </summary>
    public string[] Prefixes { get; set; } = ["/"];

    /// <summary>
    /// Allows whitespace between the prefix and command body.
    /// </summary>
    public bool AllowSpaceAfterPrefix { get; set; }

    /// <summary>
    /// Matches literal template parts without case sensitivity.
    /// </summary>
    public bool IgnoreCase { get; set; } = true;
}
