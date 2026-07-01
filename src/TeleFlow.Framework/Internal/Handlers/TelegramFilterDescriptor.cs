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

    public TelegramFilterDescriptor(
        Type customFilterType,
        Type customFilterContextType)
        : this(customFilterType)
    {
        ArgumentNullException.ThrowIfNull(customFilterContextType);

        CustomFilterContextType = customFilterContextType;
    }

    public TelegramFilterDescriptor(
        Type customFilterType,
        Attribute customFilterAttribute)
        : this(customFilterType)
    {
        ArgumentNullException.ThrowIfNull(customFilterAttribute);

        CustomFilterAttribute = customFilterAttribute;
    }

    public TelegramFilterDescriptor(
        Type customFilterType,
        Type customFilterContextType,
        Attribute customFilterAttribute)
        : this(customFilterType, customFilterContextType)
    {
        ArgumentNullException.ThrowIfNull(customFilterAttribute);

        CustomFilterAttribute = customFilterAttribute;
    }

    public TelegramFilterKind? Kind { get; }

    public Type? CustomFilterType { get; }

    public Type? CustomFilterContextType { get; }

    public Attribute? CustomFilterAttribute { get; }

    public IReadOnlyList<string> StringValues { get; }

    public IReadOnlyList<long> LongValues { get; }
}
