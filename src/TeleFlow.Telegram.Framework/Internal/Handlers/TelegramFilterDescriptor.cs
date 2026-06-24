namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed class TelegramFilterDescriptor
{
    public TelegramFilterDescriptor(
        TelegramFilterKind kind,
        IReadOnlyList<string> stringValues,
        IReadOnlyList<long> longValues)
    {
        ArgumentNullException.ThrowIfNull(stringValues);
        ArgumentNullException.ThrowIfNull(longValues);

        Kind = kind;
        StringValues = stringValues;
        LongValues = longValues;
    }

    public TelegramFilterDescriptor(Type customFilterType)
    {
        ArgumentNullException.ThrowIfNull(customFilterType);

        CustomFilterType = customFilterType;
        StringValues = [];
        LongValues = [];
    }

    public TelegramFilterKind? Kind { get; }

    public Type? CustomFilterType { get; }

    public IReadOnlyList<string> StringValues { get; }

    public IReadOnlyList<long> LongValues { get; }
}
