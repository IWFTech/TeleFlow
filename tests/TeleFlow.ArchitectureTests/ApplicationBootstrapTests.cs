using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Framework.Application;
using TeleFlow.Framework.Dispatching;
using TeleFlow.Framework.Updates;

namespace TeleFlow.ArchitectureTests;

public sealed class ApplicationBootstrapTests
{
    [Fact]
    public void CreateBuilder_ExposesServiceCollection()
    {
        var args = new[] { "--environment", "Development" };
        var builder = TeleFlowApplication.CreateBuilder(args);
        var arguments = Assert.IsType<TeleFlowApplicationArguments>(
            Assert.Single(builder.Services, descriptor => descriptor.ServiceType == typeof(TeleFlowApplicationArguments))
                .ImplementationInstance);

        args[1] = "Production";

        Assert.NotNull(builder);
        Assert.IsAssignableFrom<IServiceCollection>(builder.Services);
        Assert.Equal(["--environment", "Development"], arguments.Values);
        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ServiceType == typeof(IReadOnlyList<string>));
    }

    [Fact]
    public void Build_ReturnsApplicationAbstraction()
    {
        var builder = TeleFlowApplication.CreateBuilder();
        builder.Services.AddSingleton<IUpdateSource, NoOpUpdateSource>();
        builder.Services.AddSingleton<IUpdateDispatcher, NoOpUpdateDispatcher>();

        var application = builder.Build();

        Assert.NotNull(application);
        Assert.IsAssignableFrom<ITeleFlowApplication>(application);
    }

    [Fact]
    public void Dispose_DisposesRootServiceProviderOwnedSingletons()
    {
        var builder = TeleFlowApplication.CreateBuilder();
        builder.Services.AddSingleton<IUpdateSource, NoOpUpdateSource>();
        builder.Services.AddSingleton<DisposableProbe>();
        builder.Services.AddSingleton<IUpdateDispatcher>(provider =>
        {
            provider.GetRequiredService<DisposableProbe>();
            return new NoOpUpdateDispatcher();
        });

        var application = builder.Build();

        application.Dispose();
        application.Dispose();

        var probe = Assert.IsType<DisposableProbe>(DisposableProbe.LastInstance);
        Assert.Equal(1, probe.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_DisposesAsyncRootServiceProviderOwnedSingletons()
    {
        var builder = TeleFlowApplication.CreateBuilder();
        builder.Services.AddSingleton<IUpdateSource, NoOpUpdateSource>();
        builder.Services.AddSingleton<AsyncDisposableProbe>();
        builder.Services.AddSingleton<IUpdateDispatcher>(provider =>
        {
            provider.GetRequiredService<AsyncDisposableProbe>();
            return new NoOpUpdateDispatcher();
        });

        var application = builder.Build();

        await application.DisposeAsync();
        await application.DisposeAsync();

        var probe = Assert.IsType<AsyncDisposableProbe>(AsyncDisposableProbe.LastInstance);
        Assert.Equal(1, probe.DisposeAsyncCount);
    }

    [Fact]
    public async Task RunAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var builder = TeleFlowApplication.CreateBuilder();
        builder.Services.AddSingleton<IUpdateSource, NoOpUpdateSource>();
        builder.Services.AddSingleton<IUpdateDispatcher, NoOpUpdateDispatcher>();

        var application = builder.Build();
        application.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => application.RunAsync());
    }

    [Fact]
    public void Build_WithoutUpdateSource_ThrowsClearException()
    {
        var builder = TeleFlowApplication.CreateBuilder();
        builder.Services.AddSingleton<IUpdateDispatcher, NoOpUpdateDispatcher>();

        var exception = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Contains(nameof(IUpdateSource), exception.Message);
    }

    [Fact]
    public void Build_WithoutDispatcher_ThrowsClearException()
    {
        var builder = TeleFlowApplication.CreateBuilder();
        builder.Services.AddSingleton<IUpdateSource, NoOpUpdateSource>();

        var exception = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Contains(nameof(IUpdateDispatcher), exception.Message);
    }

    private sealed class NoOpUpdateSource : IUpdateSource
    {
        public Task StartAsync(
            Func<IUpdatePayload, CancellationToken, Task> updateHandler,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpUpdateDispatcher : IUpdateDispatcher
    {
        public Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class DisposableProbe : IDisposable
    {
        public static object? LastInstance { get; private set; }

        public DisposableProbe()
        {
            LastInstance = this;
        }

        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class AsyncDisposableProbe : IAsyncDisposable
    {
        public static object? LastInstance { get; private set; }

        public AsyncDisposableProbe()
        {
            LastInstance = this;
        }

        public int DisposeAsyncCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCount++;
            return ValueTask.CompletedTask;
        }
    }
}
