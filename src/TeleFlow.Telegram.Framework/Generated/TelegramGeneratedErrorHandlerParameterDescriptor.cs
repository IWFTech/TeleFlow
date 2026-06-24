using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure error handler parameter metadata emitted by TeleFlow source generators.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TelegramGeneratedErrorHandlerParameterDescriptor
{
    public TelegramGeneratedErrorHandlerParameterDescriptor(
        Type parameterType,
        TelegramGeneratedErrorHandlerParameterKind kind,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(parameterType);

        ParameterType = parameterType;
        Kind = kind;
        Name = name;
    }

    public Type ParameterType { get; }

    public TelegramGeneratedErrorHandlerParameterKind Kind { get; }

    public string? Name { get; }
}
