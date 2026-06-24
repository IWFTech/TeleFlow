using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure registry consumed by TeleFlow-generated handler registrars.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITelegramGeneratedHandlerRegistry
{
    void RegisterHandler(TelegramGeneratedHandlerDescriptor descriptor);

    void RegisterErrorHandler(TelegramGeneratedErrorHandlerDescriptor descriptor);
}
