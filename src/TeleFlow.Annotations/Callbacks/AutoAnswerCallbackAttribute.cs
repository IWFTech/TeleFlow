namespace TeleFlow.Annotations;
/// <summary>
/// Configures automatic answering of matched Telegram callback queries.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AutoAnswerCallbackAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Enables automatic callback answering with no text.
    /// </summary>
    public AutoAnswerCallbackAttribute()
    {
    }

    /// <summary>
    /// Enables automatic callback answering with the specified answer text.
    /// </summary>
    /// <param name="text">Text to send in the callback query answer.</param>
    public AutoAnswerCallbackAttribute(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text.Trim();
    }

    /// <summary>
    /// Text sent in the automatic callback query answer.
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// Shows the callback answer as an alert instead of a notification.
    /// </summary>
    public bool ShowAlert { get; set; }

    /// <summary>
    /// Enables or disables automatic callback answering for the annotated scope.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
