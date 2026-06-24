using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TeleFlow.Telegram;

public static class TelegramLongPollingClientServiceCollectionExtensions
{
    public static IServiceCollection AddTelegramLongPollingClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.TryAddSingleton<ITelegramLongPollingClient, TelegramLongPollingClient>();

        return services;
    }
}
