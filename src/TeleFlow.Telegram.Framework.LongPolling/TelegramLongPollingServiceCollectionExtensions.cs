using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Core.Updates;
using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Internal.Options;

namespace TeleFlow.Telegram;

public static class TelegramLongPollingServiceCollectionExtensions
{
    public static IServiceCollection AddLongPolling(
        this IServiceCollection services,
        Action<TelegramLongPollingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureNotRegistered<IUpdateSource>(services, nameof(AddLongPolling));
        EnsureTelegramBotRegistered(services, nameof(AddLongPolling));

        var options = new TelegramLongPollingOptions();
        configure?.Invoke(options);
        TelegramLongPollingOptionsValidator.Validate(options);

        services.AddTelegramLongPollingClient();
        services.AddSingleton(options);
        services.AddSingleton<IUpdateSource, TelegramLongPollingUpdateSource>();

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
