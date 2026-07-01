using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure registry consumed by TeleFlow-generated callback data codec registrars
/// when generated assemblies expose compact callback payload codecs.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITelegramGeneratedCallbackDataCodecRegistry
{
    void RegisterCallbackDataCodec(TelegramGeneratedCallbackDataCodecDescriptor descriptor);
}
