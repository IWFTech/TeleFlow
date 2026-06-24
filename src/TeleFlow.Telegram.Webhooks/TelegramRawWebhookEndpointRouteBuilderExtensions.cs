using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TeleFlow.Telegram.Webhooks.Internal;

namespace TeleFlow.Telegram.Webhooks;

public static class TelegramRawWebhookEndpointRouteBuilderExtensions
{
    public static RouteHandlerBuilder MapTelegramWebhook(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        TelegramRawWebhookHandler handler,
        Action<TelegramRawWebhookOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentNullException.ThrowIfNull(handler);

        var options = new TelegramRawWebhookOptions();
        configure?.Invoke(options);
        TelegramRawWebhookOptionsValidator.Validate(options);

        var normalizedPattern = pattern.Trim();
        if (!normalizedPattern.StartsWith('/'))
        {
            throw new InvalidOperationException("Telegram webhook path must start with '/'.");
        }

        var endpoint = new TelegramRawWebhookEndpoint(handler, options);
        return endpoints.MapPost(
            normalizedPattern,
            (Func<HttpContext, Task<IResult>>)endpoint.HandleAsync);
    }
}
