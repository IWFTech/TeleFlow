namespace TeleFlow.Annotations;
/// <summary>
/// Routes a Telegram text message by comparing it with a configured text value.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class TextAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a text route.
    /// </summary>
    /// <param name="value">Text value to match.</param>
    /// <param name="mode">Text comparison mode.</param>
    /// <param name="ignoreCase">Whether text comparison ignores case.</param>
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

    /// <summary>
    /// Text value used for route matching.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Text comparison mode.
    /// </summary>
    public TextMatchMode Mode { get; }

    /// <summary>
    /// Indicates whether text comparison ignores case.
    /// </summary>
    public bool IgnoreCase { get; }
}
