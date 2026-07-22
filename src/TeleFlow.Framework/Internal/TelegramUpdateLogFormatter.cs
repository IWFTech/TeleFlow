using TeleFlow.Telegram.Internal.Handlers;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

internal static class TelegramUpdateLogFormatter
{
    public static string GetUpdateType(Update update)
    {
        return TelegramUpdateClassifier.Classify(update).Type;
    }

    public static string FormatAllowedUpdates(IReadOnlyList<string>? allowedUpdates)
    {
        return allowedUpdates is null || allowedUpdates.Count == 0
            ? "unset"
            : $"[{string.Join(",", allowedUpdates)}]";
    }

    public static string FormatHandler(TelegramHandlerDescriptor handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return $"{handler.HandlerType.Name}.{handler.MethodName}";
    }

    public static string FormatRoute(TelegramRouteDescriptor route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return route.RouteKind switch
        {
            TelegramRouteKind.CommandExact => FormatPatternRoute(route, "CommandExact"),
            TelegramRouteKind.CommandTemplate => FormatPatternRoute(route, "CommandTemplate"),
            TelegramRouteKind.CommandRegex => FormatPatternRoute(route, "CommandRegex"),
            TelegramRouteKind.TextExact => FormatPatternRoute(route, "TextExact"),
            TelegramRouteKind.TextTemplate => FormatPatternRoute(route, "TextTemplate"),
            TelegramRouteKind.TextRegex => FormatPatternRoute(route, "TextRegex"),
            TelegramRouteKind.Callback when route.CallbackPayloadType is not null =>
                $"Callback<{route.CallbackPayloadType.Name}>",
            TelegramRouteKind.Callback => "Callback",
            TelegramRouteKind.MessageAny when route.TextFilters.Count > 0 =>
                $"MessageAny(text_filters={route.TextFilters.Count})",
            TelegramRouteKind.MessageAny => "MessageAny",
            _ => route.RouteKind.ToString()
        };
    }

    private static string FormatPatternRoute(TelegramRouteDescriptor route, string name)
    {
        return string.IsNullOrWhiteSpace(route.Pattern)
            ? name
            : $"{name}('{route.Pattern}')";
    }
}
