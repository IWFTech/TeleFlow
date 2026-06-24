namespace TeleFlow.Telegram.Internal.Handlers;

internal enum TelegramErrorHandlerParameterKind
{
    ErrorContext,
    TelegramContext,
    Exception,
    RouteValue,
    Service,
    CancellationToken
}
