using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure contract implemented by TeleFlow-generated callback data codec registrars
/// so compact callback payloads can be packed and unpacked without field reflection.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITelegramGeneratedCallbackDataCodecRegistrar
{
    void RegisterCallbackDataCodecs(ITelegramGeneratedCallbackDataCodecRegistry registry);
}
