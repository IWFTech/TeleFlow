namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AutoAnswerCallbackAttribute : TeleFlowAttribute
{
    public AutoAnswerCallbackAttribute()
    {
    }

    public AutoAnswerCallbackAttribute(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text.Trim();
    }

    public string? Text { get; }

    public bool ShowAlert { get; set; }

    public bool Enabled { get; set; } = true;
}
