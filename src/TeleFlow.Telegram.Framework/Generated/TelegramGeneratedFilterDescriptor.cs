using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure filter metadata emitted by TeleFlow source generators.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TelegramGeneratedFilterDescriptor
{
    public TelegramGeneratedFilterDescriptor(
        TelegramGeneratedFilterKind kind,
        IReadOnlyList<string>? stringValues = null,
        IReadOnlyList<long>? longValues = null)
    {
        Kind = kind;
        StringValues = CopyValues(stringValues);
        LongValues = CopyValues(longValues);
    }

    public TelegramGeneratedFilterDescriptor(Type customFilterType)
    {
        ArgumentNullException.ThrowIfNull(customFilterType);

        Kind = TelegramGeneratedFilterKind.Custom;
        CustomFilterType = customFilterType;
        StringValues = [];
        LongValues = [];
    }

    public TelegramGeneratedFilterKind Kind { get; }

    public Type? CustomFilterType { get; }

    public IReadOnlyList<string> StringValues { get; }

    public IReadOnlyList<long> LongValues { get; }

    private static TValue[] CopyValues<TValue>(IReadOnlyList<TValue>? values)
    {
        return values is null || values.Count == 0
            ? []
            : values.ToArray();
    }
}
