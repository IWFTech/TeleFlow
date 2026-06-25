using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Telegram;

namespace TeleFlow.Benchmarks.Infrastructure;

internal sealed class TeleFlowNativeClientBenchmarkRuntime : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    private TeleFlowNativeClientBenchmarkRuntime(ServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public static TeleFlowNativeClientBenchmarkRuntime Create(
        TelegramTransportResponse response,
        bool includeLongPolling = false)
    {
        ArgumentNullException.ThrowIfNull(response);

        var services = new ServiceCollection();

        services.AddTelegramClient(options => options.Token = "123:benchmark");
        services.AddSingleton(response);
        services.AddTelegramTransport<FixedTelegramTransport>();

        if (includeLongPolling)
        {
            services.AddTelegramLongPollingClient();
        }

        var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        return new TeleFlowNativeClientBenchmarkRuntime(serviceProvider);
    }

    public ITelegramClient Bot => _serviceProvider.GetRequiredService<ITelegramClient>();

    public ITelegramLongPollingClient LongPolling => _serviceProvider.GetRequiredService<ITelegramLongPollingClient>();

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
