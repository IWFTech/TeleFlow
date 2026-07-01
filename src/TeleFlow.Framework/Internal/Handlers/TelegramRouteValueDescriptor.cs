namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed class TelegramRouteValueDescriptor
{
    public TelegramRouteValueDescriptor(
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
