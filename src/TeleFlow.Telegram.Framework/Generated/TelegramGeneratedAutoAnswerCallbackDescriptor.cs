using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure auto-answer metadata emitted by TeleFlow source generators.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TelegramGeneratedAutoAnswerCallbackDescriptor
{
    public TelegramGeneratedAutoAnswerCallbackDescriptor(
        bool enabled,
        string? text = null,
        bool showAlert = false)
    {
        Enabled = enabled;
        Text = text;
        ShowAlert = showAlert;
    }

    public bool Enabled { get; }

    public string? Text { get; }

    public bool ShowAlert { get; }
}
