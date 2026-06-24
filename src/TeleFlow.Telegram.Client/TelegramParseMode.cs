namespace TeleFlow.Telegram;

public readonly record struct TelegramParseMode
{
    private readonly string? _value;

    private TelegramParseMode(string? value, bool isNone)
    {
        if (!isNone)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
        }

        _value = value;
        IsNone = isNone;
    }

    public bool IsNone { get; }

    public string? Value => IsNone ? null : _value;

    public static TelegramParseMode Html => new("HTML", isNone: false);

    public static TelegramParseMode Markdown => new("Markdown", isNone: false);

    public static TelegramParseMode MarkdownV2 => new("MarkdownV2", isNone: false);

    public static TelegramParseMode None => new(null, isNone: true);

    public static TelegramParseMode Custom(string value)
    {
        return new TelegramParseMode(value, isNone: false);
    }

    public override string ToString()
    {
        return IsNone ? nameof(None) : _value ?? string.Empty;
    }
}
