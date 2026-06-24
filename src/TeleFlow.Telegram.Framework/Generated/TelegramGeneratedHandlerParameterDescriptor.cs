using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure parameter metadata emitted by TeleFlow source generators.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TelegramGeneratedHandlerParameterDescriptor
{
    public TelegramGeneratedHandlerParameterDescriptor(
        Type parameterType,
        TelegramGeneratedHandlerParameterKind kind,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(parameterType);

        ParameterType = parameterType;
        Kind = kind;
        Name = name;
    }

    public Type ParameterType { get; }

    public TelegramGeneratedHandlerParameterKind Kind { get; }

    public string? Name { get; }
}
