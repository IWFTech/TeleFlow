using TeleFlow.Annotations;

namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed class TelegramTextFilter
{
    public TelegramTextFilter(string value, TextMatchMode mode, bool ignoreCase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value;
        Mode = mode;
        IgnoreCase = ignoreCase;
    }

    public string Value { get; }

    public TextMatchMode Mode { get; }

    public bool IgnoreCase { get; }

    public bool Matches(string? text)
    {
        if (text is null)
        {
            return false;
        }

        var comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        return Mode switch
        {
            TextMatchMode.Equals => string.Equals(text, Value, comparison),
            TextMatchMode.StartsWith => text.StartsWith(Value, comparison),
            TextMatchMode.Contains => text.Contains(Value, comparison),
            _ => throw new InvalidOperationException($"Unsupported text match mode '{Mode}'.")
        };
    }
}
