namespace TeleFlow.Annotations;
/// <summary>
/// Routes a Telegram text message by matching it with a regular expression.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class TextRegexAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a text regex route.
    /// </summary>
    /// <param name="pattern">Regular expression pattern applied to the message text.</param>
    public TextRegexAttribute(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        Pattern = pattern;
    }

    /// <summary>
    /// Regular expression pattern applied to the message text.
    /// </summary>
    public string Pattern { get; }

    /// <summary>
    /// Matches the message text without case sensitivity.
    /// </summary>
    public bool IgnoreCase { get; set; } = true;
}
