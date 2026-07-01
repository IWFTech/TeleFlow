using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure attribute emitted by TeleFlow source generators to identify generated Telegram handler registrars.
/// This API is not intended to be used by application code.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TelegramGeneratedHandlersAttribute : Attribute
{
    public TelegramGeneratedHandlersAttribute(Type registrarType)
    {
        ArgumentNullException.ThrowIfNull(registrarType);

        RegistrarType = registrarType;
    }

    public Type RegistrarType { get; }
}
