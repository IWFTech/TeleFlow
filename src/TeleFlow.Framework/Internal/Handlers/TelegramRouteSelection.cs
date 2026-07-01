namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed class TelegramRouteSelection
{
    public TelegramRouteSelection(
        TelegramHandlerDescriptor handler,
        TelegramRouteDescriptor route,
        IReadOnlyDictionary<string, object?> routeValues,
        object? callbackPayload)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(routeValues);

        Handler = handler;
        Route = route;
        RouteValues = routeValues;
        CallbackPayload = callbackPayload;
    }

    public TelegramHandlerDescriptor Handler { get; }

    public TelegramRouteDescriptor Route { get; }

    public IReadOnlyDictionary<string, object?> RouteValues { get; }

    public object? CallbackPayload { get; }
}
