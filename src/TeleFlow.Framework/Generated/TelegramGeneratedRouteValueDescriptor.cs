using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure route value metadata emitted by TeleFlow source generators.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TelegramGeneratedRouteValueDescriptor
{
    public TelegramGeneratedRouteValueDescriptor(
        string name,
        Type valueType,
        bool isOptional = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(valueType);

        Name = name;
        ValueType = valueType;
        IsOptional = isOptional;
    }

    public string Name { get; }

    public Type ValueType { get; }

    public bool IsOptional { get; }
}
