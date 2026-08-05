using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TeleFlow.Telegram.Internal;

namespace TeleFlow.Telegram;

public static class TelegramLongPollingClientServiceCollectionExtensions
{
    public static IServiceCollection AddTelegramLongPollingClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.TryAddSingleton<ITelegramUpdateDecodeFailurePolicy>(StopTelegramUpdateDecodeFailurePolicy.Instance);
        services.TryAddSingleton<ITelegramLongPollingClient>(static provider =>
        {
            var client = provider.GetRequiredService<ITelegramClient>();
            var batchReceiver = client is TelegramClient
                ? provider.GetRequiredService<ITelegramUpdateBatchReceiver>()
                : new TypedTelegramUpdateBatchReceiver(client);

            return new TelegramLongPollingClient(
                batchReceiver,
                provider.GetRequiredService<ITelegramUpdateDecodeFailurePolicy>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ILoggerFactory>());
        });

        return services;
    }
}
