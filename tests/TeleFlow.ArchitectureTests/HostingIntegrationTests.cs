using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TeleFlow.Core.Application;
using TeleFlow.Core.DependencyInjection;
using TeleFlow.Core.Dispatching;
using TeleFlow.Core.Middleware;
using TeleFlow.Core.Updates;
using TeleFlow.Hosting;

namespace TeleFlow.ArchitectureTests;

public sealed class HostingIntegrationTests
{
    [Fact]
    public void AddTeleFlowHostedService_RegistersSingleTeleFlowHostedService()
    {
        var services = CreateBaseServices();

        services.AddTeleFlowHostedService();
        services.AddTeleFlowHostedService();

        using var provider = BuildProvider(services);

        Assert.Single(provider.GetServices<IHostedService>());
    }

    [Fact]
    public void AddTeleFlowHostedService_DoesNotRemoveExistingHostedServices()
    {
        var services = CreateBaseServices();

        services.AddSingleton<IHostedService, OtherHostedService>();
        services.AddTeleFlowHostedService();

        using var provider = BuildProvider(services);

        Assert.Equal(2, provider.GetServices<IHostedService>().Count());
    }

    [Fact]
    public async Task HostedService_StartsApplicationAndStopCancelsUpdateSource()
    {
        var services = CreateBaseServices();
        var trace = new List<string>();

        services.AddSingleton(trace);
        services.AddSingleton<BlockingUpdateSource>();
        services.AddSingleton<IUpdateSource>(static provider => provider.GetRequiredService<BlockingUpdateSource>());
        services.AddTeleFlowShutdownTask<RecordingShutdownTask>();
        services.AddTeleFlowHostedService();

        using var provider = BuildProvider(services);
        var source = provider.GetRequiredService<BlockingUpdateSource>();
        var hostedService = provider.GetRequiredService<IHostedService>();

        await hostedService.StartAsync(CancellationToken.None);
        await source.Started.Task.WaitAsync(TestTimeout);

        await hostedService.StopAsync(CancellationToken.None);

        await source.CancellationObserved.Task.WaitAsync(TestTimeout);
        Assert.Equal(["shutdown"], trace);
    }

    [Fact]
    public async Task HostedService_DoesNotDisposeHostOwnedServiceProvider()
    {
        var services = CreateBaseServices();

        services.AddSingleton<DisposableProbe>();
        services.AddSingleton<BlockingUpdateSource>();
        services.AddSingleton<IUpdateSource>(static provider => provider.GetRequiredService<BlockingUpdateSource>());
        services.AddTeleFlowHostedService();

        var provider = BuildProvider(services);
        var probe = provider.GetRequiredService<DisposableProbe>();
        var source = provider.GetRequiredService<BlockingUpdateSource>();
        var hostedService = provider.GetRequiredService<IHostedService>();

        await hostedService.StartAsync(CancellationToken.None);
        await source.Started.Task.WaitAsync(TestTimeout);
        await hostedService.StopAsync(CancellationToken.None);

        Assert.False(probe.IsDisposed);

        provider.Dispose();

        Assert.True(probe.IsDisposed);
    }

    [Fact]
    public async Task HostedService_ExposesStartupTaskFailureThroughBackgroundServiceTask()
    {
        var services = CreateBaseServices();
        var exception = new InvalidOperationException("startup failed");

        services.AddSingleton(exception);
        services.AddSingleton<IUpdateSource, CompletingUpdateSource>();
        services.AddTeleFlowStartupTask<ThrowingStartupTask>();
        services.AddTeleFlowHostedService();

        using var provider = BuildProvider(services);
        var hostedService = provider.GetRequiredService<IHostedService>();

        await hostedService.StartAsync(CancellationToken.None);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => GetExecuteTask(hostedService));

        Assert.Same(exception, thrown);
    }

    [Fact]
    public async Task HostedService_ExposesDirectRegistrationValidationThroughBackgroundServiceTask()
    {
        var services = CreateBaseServices();

        services.AddSingleton<IUpdateSource, CompletingUpdateSource>();
        services.AddTeleFlowHostedService();
        services.AddScoped<IUpdateMiddleware, DirectMiddleware>();

        using var provider = BuildProvider(services);
        var hostedService = provider.GetRequiredService<IHostedService>();

        await hostedService.StartAsync(CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => GetExecuteTask(hostedService));

        Assert.Contains(nameof(IUpdateMiddleware), exception.Message);
        Assert.Contains(nameof(ServiceCollectionMiddlewareExtensions.AddUpdateMiddleware), exception.Message);
        Assert.Contains(nameof(ServiceCollectionMiddlewareExtensions.AddSingletonUpdateMiddleware), exception.Message);
    }

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    private static IServiceCollection CreateBaseServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IUpdateDispatcher, NoOpDispatcher>();

        return services;
    }

    private static ServiceProvider BuildProvider(IServiceCollection services)
    {
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static Task GetExecuteTask(IHostedService hostedService)
    {
        var backgroundService = Assert.IsAssignableFrom<BackgroundService>(hostedService);

        return backgroundService.ExecuteTask
            ?? throw new InvalidOperationException("The background service has not been started.");
    }

    private sealed class BlockingUpdateSource : IUpdateSource
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task StartAsync(
            Func<IUpdatePayload, CancellationToken, Task> updateHandler,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(updateHandler);

            Started.TrySetResult();

            await using var registration = cancellationToken.Register(
                static state => ((TaskCompletionSource)state!).TrySetResult(),
                CancellationObserved);

            await CancellationObserved.Task.ConfigureAwait(false);
        }
    }

    private sealed class CompletingUpdateSource : IUpdateSource
    {
        public Task StartAsync(
            Func<IUpdatePayload, CancellationToken, Task> updateHandler,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(updateHandler);

            return Task.CompletedTask;
        }
    }

    private sealed class NoOpDispatcher : IUpdateDispatcher
    {
        public Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingShutdownTask(List<string> trace) : ITeleFlowShutdownTask
    {
        public ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            trace.Add("shutdown");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingStartupTask(InvalidOperationException exception) : ITeleFlowStartupTask
    {
        public ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException(exception);
        }
    }

    private sealed class DirectMiddleware : IUpdateMiddleware
    {
        public Task InvokeAsync(UpdateContext context, UpdateDelegate next)
        {
            return next(context);
        }
    }

    private sealed class OtherHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class DisposableProbe : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
