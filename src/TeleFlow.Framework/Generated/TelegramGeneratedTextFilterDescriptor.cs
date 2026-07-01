using System.ComponentModel;
using TeleFlow.Annotations;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure text filter metadata emitted by TeleFlow source generators.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TelegramGeneratedTextFilterDescriptor
{
    public TelegramGeneratedTextFilterDescriptor(
        string value,
        TextMatchMode mode,
        bool ignoreCase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value;
        Mode = mode;
        IgnoreCase = ignoreCase;
    }

    public string Value { get; }

    public TextMatchMode Mode { get; }

    public bool IgnoreCase { get; }
}
