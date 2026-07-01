using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure attribute emitted by TeleFlow source generators to identify generated
/// callback data codec registrars for an assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TelegramGeneratedCallbackDataCodecsAttribute : Attribute
{
    public TelegramGeneratedCallbackDataCodecsAttribute(Type registrarType)
    {
        ArgumentNullException.ThrowIfNull(registrarType);

        RegistrarType = registrarType;
    }

    public Type RegistrarType { get; }
}
