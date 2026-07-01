using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Annotations;
using TeleFlow.Benchmarks.Handlers;
using TeleFlow.Framework.Dispatching;
using TeleFlow.Framework.States;
using TeleFlow.Framework.Updates;
using TeleFlow.Storage.Memory;
using TeleFlow.Telegram;

namespace TeleFlow.Benchmarks.Infrastructure;

internal sealed class TeleFlowBenchmarkRuntime : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    private TeleFlowBenchmarkRuntime(ServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public static TeleFlowBenchmarkRuntime Create()
    {
        var services = new ServiceCollection();

        services.AddTelegramBot(options => options.Token = "benchmark-token");
        services.AddMemoryStateStorage();
        services.AddTelegramTransport<FixedTelegramTransport>();
        services.AddSingleton(TelegramTransportResponses.SendMessageOk());
        services.AddSingleton<BenchmarkProbe>();
        services.AddTelegramHandler<BenchmarkCommandHandler>();
        services.AddTelegramHandler<BenchmarkCallbackHandler>();
        services.AddTelegramHandler<BenchmarkStateHandler>();

        var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        return new TeleFlowBenchmarkRuntime(serviceProvider);
    }

    public ITelegramClient Bot => _serviceProvider.GetRequiredService<ITelegramClient>();

    public async Task DispatchAsync(TelegramUpdatePayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        using var scope = _serviceProvider.CreateScope();
        var context = new UpdateContext(scope.ServiceProvider, payload, cancellationToken);

        await scope.ServiceProvider
            .GetRequiredService<IUpdateDispatcher>()
            .DispatchAsync(context, cancellationToken)
            .ConfigureAwait(false);
    }

    public ValueTask SetBenchmarkStateAsync(string state, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        return _serviceProvider
            .GetRequiredService<IStateStore>()
            .SetStateAsync(BenchmarkStateKeys.DefaultUserChat, state, cancellationToken);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}

internal static class BenchmarkStateKeys
{
    public static readonly StateKey DefaultUserChat = StateKey.Create(
        scope: "telegram",
        subject: "user:3000001",
        partition: "chat:2000001");
}

internal static class BenchmarkStates
{
    public const string AwaitingName = "benchmark:awaiting-name";
}
