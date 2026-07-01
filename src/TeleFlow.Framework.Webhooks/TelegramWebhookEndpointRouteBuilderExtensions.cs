using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Framework.Updates;

namespace TeleFlow.Telegram.Webhooks;

public static class TelegramWebhookEndpointRouteBuilderExtensions
{
    public static RouteHandlerBuilder MapTelegramWebhook(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<TelegramWebhookOptions>();
        var processor = endpoints.ServiceProvider.GetRequiredService<IUpdateProcessor>();

        return endpoints.MapTelegramWebhook(
            options.Path,
            async (update, _, cancellationToken) =>
            {
                await processor.ProcessAsync(new TelegramUpdatePayload(update), cancellationToken).ConfigureAwait(false);
                return Results.Ok();
            },
            rawOptions => rawOptions.SecretToken = options.SecretToken);
    }
}
