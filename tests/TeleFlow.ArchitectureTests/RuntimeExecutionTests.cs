using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Core.Application;
using TeleFlow.Core.Dispatching;
using TeleFlow.Core.Middleware;
using TeleFlow.Core.Updates;

namespace TeleFlow.ArchitectureTests;

public sealed class RuntimeExecutionTests
{
    [Fact]
    public async Task RunAsync_StartsRegisteredUpdateSource()
    {
        var source = new RecordingUpdateSource([new TestUpdatePayload("first")]);
        var dispatcher = new RecordingDispatcher();

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
            });

        await application.RunAsync();

        Assert.Equal(1, source.StartCallCount);
    }

    [Fact]
    public async Task RunAsync_ProcessesEachPayloadThroughDispatcher()
    {
        var source = new RecordingUpdateSource(
        [
            new TestUpdatePayload("first"),
            new TestUpdatePayload("second"),
            new TestUpdatePayload("third")
        ]);
        var dispatcher = new RecordingDispatcher();

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
            });

        await application.RunAsync();

        Assert.Equal(3, dispatcher.InvocationCount);
        Assert.Collection(
            dispatcher.Contexts,
            context => Assert.Equal("first", ((TestUpdatePayload)context.Payload).Name),
            context => Assert.Equal("second", ((TestUpdatePayload)context.Payload).Name),
            context => Assert.Equal("third", ((TestUpdatePayload)context.Payload).Name));
    }

    [Fact]
    public async Task Middleware_ExecutesInRegistrationOrder()
    {
        var trace = new List<string>();
        var source = new RecordingUpdateSource([new TestUpdatePayload("only")]);
        var dispatcher = new RecordingDispatcher(trace);

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton(trace);
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddUpdateMiddleware<FirstProbeMiddleware>();
                services.AddUpdateMiddleware<SecondProbeMiddleware>();
            });

        await application.RunAsync();

        Assert.Equal(
        [
            "enter:mw-1",
            "enter:mw-2",
            "dispatch",
            "exit:mw-2",
            "exit:mw-1"
        ], trace);
    }

    [Fact]
    public async Task Middleware_CanShortCircuitDispatcher()
    {
        var trace = new List<string>();
        var source = new RecordingUpdateSource([new TestUpdatePayload("only")]);
        var dispatcher = new RecordingDispatcher(trace);

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton(trace);
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddUpdateMiddleware<ShortCircuitMiddleware>();
            });

        await application.RunAsync();

        Assert.Equal(0, dispatcher.InvocationCount);
        Assert.Equal(["enter:stop", "short-circuit:stop"], trace);
    }

    [Fact]
    public void Build_FailsClearlyForDirectMiddlewareServiceRegistration()
    {
        var builder = TeleFlowApplication.CreateBuilder();

        builder.Services.AddSingleton<IUpdateSource>(new RecordingUpdateSource([]));
        builder.Services.AddSingleton<IUpdateDispatcher>(new RecordingDispatcher());
        builder.Services.AddSingleton<IUpdateMiddleware, DirectMiddleware>();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains(nameof(IUpdateMiddleware), exception.Message);
        Assert.Contains(nameof(ServiceCollectionMiddlewareExtensions.AddUpdateMiddleware), exception.Message);
        Assert.Contains(nameof(ServiceCollectionMiddlewareExtensions.AddSingletonUpdateMiddleware), exception.Message);
    }

    [Fact]
    public async Task Dispatcher_ReceivesSameContextSeenByMiddleware()
    {
        var source = new RecordingUpdateSource([new TestUpdatePayload("only")]);
        var dispatcher = new RecordingDispatcher();
        var recorder = new ContextRecorder();

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton(recorder);
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddUpdateMiddleware<ContextRecordingMiddleware>();
            });

        await application.RunAsync();

        Assert.Same(recorder.Context, dispatcher.Contexts.Single());
    }

    [Fact]
    public async Task AddUpdateMiddleware_AllowsScopedConstructorDependencies()
    {
        var source = new RecordingUpdateSource([new TestUpdatePayload("only")]);
        var dispatcher = new ScopedProbeDispatcher();
        var recorder = new ScopedProbeRecorder();

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton(recorder);
                services.AddScoped<ScopedProbe>();
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddUpdateMiddleware<ScopedConstructorMiddleware>();
            });

        await application.RunAsync();

        Assert.Equal(dispatcher.ProbeIds.Single(), recorder.MiddlewareProbeIds.Single());
    }

    [Fact]
    public async Task EachUpdate_GetsDistinctScopedServiceInstance()
    {
        var source = new RecordingUpdateSource(
        [
            new TestUpdatePayload("first"),
            new TestUpdatePayload("second")
        ]);
        var dispatcher = new ScopedProbeDispatcher();
        var recorder = new ScopedProbeRecorder();

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton(recorder);
                services.AddScoped<ScopedProbe>();
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddUpdateMiddleware<ScopedConstructorMiddleware>();
            });

        await application.RunAsync();

        Assert.Collection(
            recorder.MiddlewareProbeIds.Zip(dispatcher.ProbeIds),
            pair => Assert.Equal(pair.First, pair.Second),
            pair => Assert.Equal(pair.First, pair.Second));
        Assert.NotEqual(recorder.MiddlewareProbeIds[0], recorder.MiddlewareProbeIds[1]);
    }

    [Fact]
    public async Task AddSingletonUpdateMiddleware_ReusesMiddlewareAcrossUpdates()
    {
        var source = new RecordingUpdateSource(
        [
            new TestUpdatePayload("first"),
            new TestUpdatePayload("second")
        ]);
        var dispatcher = new RecordingDispatcher();
        var recorder = new SingletonMiddlewareRecorder();

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton(recorder);
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddSingletonUpdateMiddleware<SingletonRecordingMiddleware>();
            });

        await application.RunAsync();

        Assert.Equal(2, recorder.MiddlewareInstanceIds.Count);
        Assert.Equal(recorder.MiddlewareInstanceIds[0], recorder.MiddlewareInstanceIds[1]);
    }

    [Fact]
    public async Task CancellationToken_FlowsToSourceAndContext()
    {
        var source = new RecordingUpdateSource([new TestUpdatePayload("only")]);
        var dispatcher = new RecordingDispatcher();
        using var cancellation = new CancellationTokenSource();

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
            });

        await application.RunAsync(cancellation.Token);

        Assert.Equal(cancellation.Token, source.ReceivedCancellationToken);
        Assert.Equal(cancellation.Token, dispatcher.Contexts.Single().CancellationToken);
    }

    [Fact]
    public async Task MiddlewareException_BubblesOutOfRunAsync()
    {
        var source = new RecordingUpdateSource([new TestUpdatePayload("only")]);
        var dispatcher = new RecordingDispatcher();
        var exception = new InvalidOperationException("middleware exploded");

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddSingleton(exception);
                services.AddUpdateMiddleware<ThrowingMiddleware>();
            });

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => application.RunAsync());

        Assert.Same(exception, thrown);
    }

    [Fact]
    public async Task DispatcherException_BubblesOutOfRunAsync()
    {
        var source = new RecordingUpdateSource([new TestUpdatePayload("only")]);
        var exception = new InvalidOperationException("dispatcher exploded");
        var dispatcher = new ThrowingDispatcher(exception);

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
            });

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => application.RunAsync());

        Assert.Same(exception, thrown);
    }

    [Fact]
    public async Task UpdateProcessor_ProcessesPayloadThroughMiddlewareAndDispatcher()
    {
        var trace = new List<string>();
        var dispatcher = new RecordingDispatcher(trace);
        var services = new ServiceCollection();

        services.AddSingleton(trace);
        services.AddSingleton<IUpdateDispatcher>(dispatcher);
        services.AddUpdateMiddleware<FirstProbeMiddleware>();
        services.AddUpdateMiddleware<SecondProbeMiddleware>();
        services.AddSingleton<IUpdateProcessor, DefaultUpdateProcessor>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var processor = provider.GetRequiredService<IUpdateProcessor>();

        await processor.ProcessAsync(new TestUpdatePayload("processor"));

        Assert.Equal(
        [
            "enter:mw-1",
            "enter:mw-2",
            "dispatch",
            "exit:mw-2",
            "exit:mw-1"
        ], trace);
        Assert.Equal("processor", ((TestUpdatePayload)dispatcher.Contexts.Single().Payload).Name);
    }

    [Fact]
    public async Task UpdateProcessor_CreatesPerUpdateScope()
    {
        var dispatcher = new RecordingDispatcher();
        var services = new ServiceCollection();

        var recorder = new ScopedProbeRecorder();
        services.AddSingleton(recorder);
        services.AddScoped<ScopedProbe>();
        services.AddSingleton<IUpdateDispatcher>(dispatcher);
        services.AddUpdateMiddleware<ScopedConstructorMiddleware>();
        services.AddSingleton<IUpdateProcessor, DefaultUpdateProcessor>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var processor = provider.GetRequiredService<IUpdateProcessor>();

        await processor.ProcessAsync(new TestUpdatePayload("first"));
        await processor.ProcessAsync(new TestUpdatePayload("second"));

        Assert.NotEqual(recorder.MiddlewareProbeIds[0], recorder.MiddlewareProbeIds[1]);
    }

    private static ITeleFlowApplication CreateApplication(Action<IServiceCollection> configureServices)
    {
        var builder = TeleFlowApplication.CreateBuilder();
        configureServices(builder.Services);
        return builder.Build();
    }

    private sealed record TestUpdatePayload(string Name) : IUpdatePayload;

    private sealed class RecordingUpdateSource(IReadOnlyList<IUpdatePayload> payloads) : IUpdateSource
    {
        public int StartCallCount { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public async Task StartAsync(
            Func<IUpdatePayload, CancellationToken, Task> updateHandler,
            CancellationToken cancellationToken = default)
        {
            StartCallCount++;
            ReceivedCancellationToken = cancellationToken;

            foreach (var payload in payloads)
            {
                await updateHandler(payload, cancellationToken);
            }
        }
    }

    private sealed class RecordingDispatcher : IUpdateDispatcher
    {
        private readonly List<string>? _trace;

        public RecordingDispatcher(List<string>? trace = null)
        {
            _trace = trace;
        }

        public int InvocationCount => Contexts.Count;

        public List<UpdateContext> Contexts { get; } = [];

        public Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            _trace?.Add("dispatch");
            Contexts.Add(context);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDispatcher(Exception exception) : IUpdateDispatcher
    {
        public Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromException(exception);
        }
    }

    private sealed class ScopedProbeDispatcher : IUpdateDispatcher
    {
        public List<Guid> ProbeIds { get; } = [];

        public Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            ProbeIds.Add(context.Services.GetRequiredService<ScopedProbe>().Id);
            return Task.CompletedTask;
        }
    }

    private abstract class ProbeMiddleware : IUpdateMiddleware
    {
        private readonly string _name;
        private readonly List<string> _trace;
        private readonly bool _shortCircuit;

        protected ProbeMiddleware(string name, List<string> trace, bool shortCircuit = false)
        {
            _name = name;
            _trace = trace;
            _shortCircuit = shortCircuit;
        }

        public async Task InvokeAsync(UpdateContext context, UpdateDelegate next)
        {
            _trace.Add($"enter:{_name}");

            if (_shortCircuit)
            {
                _trace.Add($"short-circuit:{_name}");
                return;
            }

            await next(context);
            _trace.Add($"exit:{_name}");
        }
    }

    private sealed class FirstProbeMiddleware(List<string> trace) : ProbeMiddleware("mw-1", trace);

    private sealed class SecondProbeMiddleware(List<string> trace) : ProbeMiddleware("mw-2", trace);

    private sealed class ShortCircuitMiddleware(List<string> trace) : ProbeMiddleware("stop", trace, shortCircuit: true);

    private sealed class ContextRecordingMiddleware(ContextRecorder recorder) : IUpdateMiddleware
    {
        public Task InvokeAsync(UpdateContext context, UpdateDelegate next)
        {
            recorder.Context = context;
            return next(context);
        }
    }

    private sealed class DirectMiddleware : IUpdateMiddleware
    {
        public Task InvokeAsync(UpdateContext context, UpdateDelegate next)
        {
            return next(context);
        }
    }

    private sealed class ScopedConstructorMiddleware(
        ScopedProbe probe,
        ScopedProbeRecorder recorder) : IUpdateMiddleware
    {
        public Task InvokeAsync(UpdateContext context, UpdateDelegate next)
        {
            recorder.MiddlewareProbeIds.Add(probe.Id);
            return next(context);
        }
    }

    private sealed class SingletonRecordingMiddleware(SingletonMiddlewareRecorder recorder) : IUpdateMiddleware
    {
        private readonly Guid _instanceId = Guid.NewGuid();

        public Task InvokeAsync(UpdateContext context, UpdateDelegate next)
        {
            recorder.MiddlewareInstanceIds.Add(_instanceId);
            return next(context);
        }
    }

    private sealed class ThrowingMiddleware(InvalidOperationException exception) : IUpdateMiddleware
    {
        public Task InvokeAsync(UpdateContext context, UpdateDelegate next)
        {
            return Task.FromException(exception);
        }
    }

    private sealed class ContextRecorder
    {
        public UpdateContext? Context { get; set; }
    }

    private sealed class ScopedProbeRecorder
    {
        public List<Guid> MiddlewareProbeIds { get; } = [];
    }

    private sealed class SingletonMiddlewareRecorder
    {
        public List<Guid> MiddlewareInstanceIds { get; } = [];
    }

    private sealed class ScopedProbe
    {
        public Guid Id { get; } = Guid.NewGuid();
    }
}
