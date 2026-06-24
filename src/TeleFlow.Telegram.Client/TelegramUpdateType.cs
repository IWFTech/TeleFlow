namespace TeleFlow.Telegram;

public readonly partial record struct TelegramUpdateType
{
    public TelegramUpdateType(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public static TelegramUpdateType Custom(string value)
    {
        return new TelegramUpdateType(value);
    }

    public override string ToString()
    {
        return Value ?? string.Empty;
    }
}
