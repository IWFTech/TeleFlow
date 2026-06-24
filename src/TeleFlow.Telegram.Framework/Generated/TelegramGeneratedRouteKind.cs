using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure route shape emitted by TeleFlow source generators.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum TelegramGeneratedRouteKind
{
    MessageAny,
    TextExact,
    CommandExact,
    TextTemplate,
    CommandTemplate,
    TextRegex,
    CommandRegex,
    Callback,
    ChatMemberUpdated,
    MyChatMemberUpdated
}
