using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TeleFlow.Framework.Updates;
using TeleFlow.Telegram.Webhooks.Internal;

namespace TeleFlow.Telegram.Webhooks;

public static class TelegramWebhookServiceCollectionExtensions
{
    public static IServiceCollection AddWebhook(
        this IServiceCollection services,
        Action<TelegramWebhookOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureNotRegistered<IUpdateSource>(services, nameof(AddWebhook));
        EnsureTelegramBotRegistered(services, nameof(AddWebhook));

        var options = new TelegramWebhookOptions();
        configure?.Invoke(options);
        TelegramWebhookOptionsValidator.Validate(options);

        services.AddSingleton(options);
        services.TryAddSingleton<IUpdateProcessor, DefaultUpdateProcessor>();
        services.AddSingleton<IUpdateSource, TelegramWebhookUpdateSource>();

        return services;
    }

    private static void EnsureTelegramBotRegistered(IServiceCollection services, string apiName)
    {
        if (services.All(static descriptor => descriptor.ServiceType != typeof(TelegramBotOptions)))
        {
            throw new InvalidOperationException(
                $"{nameof(TelegramServiceCollectionExtensions.AddTelegramBot)} must be called before {apiName}.");
        }
    }

    private static void EnsureNotRegistered<TService>(IServiceCollection services, string apiName)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(TService)))
        {
            throw new InvalidOperationException(
                $"{apiName} cannot be called more than once for service '{typeof(TService).Name}'.");
        }
    }
}
