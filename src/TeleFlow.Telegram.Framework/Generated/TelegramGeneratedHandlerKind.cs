using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure route kind emitted by TeleFlow source generators.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum TelegramGeneratedHandlerKind
{
    Command,
    Message,
    Callback,
    ChatMember
}
