using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure contract implemented by TeleFlow-generated handler registrars.
/// This API is not intended to be implemented by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITelegramGeneratedHandlerRegistrar
{
    void Register(ITelegramGeneratedHandlerRegistry registry);
}
