namespace TeleFlow.Telegram.Internal.Handlers;

internal enum TelegramRouteKind
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
