namespace TeleFlow.Telegram.Internal.Handlers;

internal enum TelegramHandlerParameterKind
{
    Context,
    CallbackPayload,
    RouteValue,
    Service,
    CancellationToken
}
