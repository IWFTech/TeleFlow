using System.Reflection;

namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed class TelegramErrorHandlerParameterDescriptor
{
    public TelegramErrorHandlerParameterDescriptor(
        ParameterInfo parameter,
        TelegramErrorHandlerParameterKind kind)
        : this(parameter.ParameterType, kind, parameter.Name)
    {
        ArgumentNullException.ThrowIfNull(parameter);
    }

    public TelegramErrorHandlerParameterDescriptor(
        Type parameterType,
        TelegramErrorHandlerParameterKind kind,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(parameterType);

        ParameterType = parameterType;
        Kind = kind;
        Name = name;
    }

    public Type ParameterType { get; }

    public TelegramErrorHandlerParameterKind Kind { get; }

    public string? Name { get; }
}
