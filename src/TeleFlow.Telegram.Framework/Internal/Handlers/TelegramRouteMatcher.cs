using System.Text.RegularExpressions;

namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed class TelegramRouteMatcher
{
    private TelegramRouteMatcher(Regex? regex)
    {
        Regex = regex;
    }

    public Regex? Regex { get; }

    public static TelegramRouteMatcher Create(
        TelegramRouteKind routeKind,
        string? pattern,
        bool ignoreCase)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return new TelegramRouteMatcher(regex: null);
        }

        return routeKind switch
        {
            TelegramRouteKind.TextTemplate or TelegramRouteKind.CommandTemplate =>
                new TelegramRouteMatcher(TelegramTemplateRouteParser.BuildRegex(pattern, ignoreCase)),
            TelegramRouteKind.TextRegex or TelegramRouteKind.CommandRegex =>
                new TelegramRouteMatcher(new Regex(pattern, TelegramTemplateRouteParser.GetRegexOptions(ignoreCase))),
            _ => new TelegramRouteMatcher(regex: null)
        };
    }
}
