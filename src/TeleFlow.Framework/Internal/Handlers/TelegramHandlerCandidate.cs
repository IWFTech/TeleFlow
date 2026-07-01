namespace TeleFlow.Telegram.Internal.Handlers;

/// <summary>
/// Represents a prepared handler route candidate selected from the handler table before per-update route
/// matching, filter evaluation, and handler invocation.
/// </summary>
internal readonly record struct TelegramHandlerCandidate(
    TelegramHandlerDescriptor Handler,
    TelegramRouteDescriptor Route,
    TelegramRouteFilterPlan Filters)
{
    public static TelegramHandlerCandidate Create(TelegramHandlerDescriptor handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return new TelegramHandlerCandidate(
            handler,
            handler.Route,
            TelegramRouteFilterPlan.Create(handler.Route));
    }
}
