namespace TeleFlow.Annotations;
/// <summary>
/// Routes a Telegram command message by matching the command body with a regular expression.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class CommandRegexAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a command regex route.
    /// </summary>
    /// <param name="pattern">Regular expression pattern applied to the command body.</param>
    public CommandRegexAttribute(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        Pattern = pattern;
    }

    /// <summary>
    /// Regular expression pattern applied to the command body.
    /// </summary>
    public string Pattern { get; }

    /// <summary>
    /// Command prefixes accepted by this route. Defaults to <c>/</c>.
    /// </summary>
    public string[] Prefixes { get; set; } = ["/"];

    /// <summary>
    /// Defines whether the command prefix is required, optional, or disabled for this route.
    /// </summary>
    public CommandPrefixMode PrefixMode { get; set; } = CommandPrefixMode.Required;

    /// <summary>
    /// Allows whitespace between the prefix and command body.
    /// </summary>
    public bool AllowSpaceAfterPrefix { get; set; }

    /// <summary>
    /// Matches the command body without case sensitivity.
    /// </summary>
    public bool IgnoreCase { get; set; } = true;
}
