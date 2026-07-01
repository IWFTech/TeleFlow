using System.Reflection;

namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed class TelegramHandlerParameterDescriptor
{
    public TelegramHandlerParameterDescriptor(
        ParameterInfo parameter,
        TelegramHandlerParameterKind kind)
        : this(parameter.ParameterType, kind, parameter.Name)
    {
        ArgumentNullException.ThrowIfNull(parameter);
    }

    public TelegramHandlerParameterDescriptor(
        Type parameterType,
        TelegramHandlerParameterKind kind,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(parameterType);

        ParameterType = parameterType;
        Kind = kind;
        Name = name;
    }

    public Type ParameterType { get; }

    public TelegramHandlerParameterKind Kind { get; }

    public string? Name { get; }
}
