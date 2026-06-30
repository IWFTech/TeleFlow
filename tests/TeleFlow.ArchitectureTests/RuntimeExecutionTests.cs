using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Core.Application;
using TeleFlow.Core.DependencyInjection;
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
    public async Task StartupTasks_ExecuteBeforeUpdateSourceInRegistrationOrder()
    {
        var trace = new List<string>();
        var source = new RecordingUpdateSource([new TestUpdatePayload("only")], trace);
        var dispatcher = new RecordingDispatcher(trace);

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton(trace);
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddTeleFlowStartupTask<FirstStartupTask>();
                services.AddTeleFlowStartupTask<SecondStartupTask>();
            });

        await application.RunAsync();

        Assert.Equal(
        [
            "startup:1",
            "startup:2",
            "source:start",
            "dispatch"
        ], trace);
    }

    [Fact]
    public async Task StartupTaskFailure_PreventsUpdateSourceAndShutdownTasks()
    {
        var trace = new List<string>();
        var source = new RecordingUpdateSource([], trace);
        var dispatcher = new RecordingDispatcher(trace);
        var exception = new InvalidOperationException("startup failed");

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton(trace);
                services.AddSingleton(exception);
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddTeleFlowStartupTask<ThrowingStartupTask>();
                services.AddTeleFlowShutdownTask<FirstShutdownTask>();
            });

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => application.RunAsync());

        Assert.Same(exception, thrown);
        Assert.Equal(0, source.StartCallCount);
        Assert.Equal(["startup:throw"], trace);
    }

    [Fact]
    public async Task ShutdownTasks_ExecuteAfterUpdateSourceInReverseRegistrationOrder()
    {
        var trace = new List<string>();
        var source = new RecordingUpdateSource([], trace);
        var dispatcher = new RecordingDispatcher(trace);

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton(trace);
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddTeleFlowShutdownTask<FirstShutdownTask>();
                services.AddTeleFlowShutdownTask<SecondShutdownTask>();
            });

        await application.RunAsync();

        Assert.Equal(
        [
            "source:start",
            "shutdown:2",
            "shutdown:1"
        ], trace);
    }

    [Fact]
    public async Task ShutdownTasks_RunWhenUpdateSourceFails()
    {
        var trace = new List<string>();
        var exception = new InvalidOperationException("source failed");
        var source = new ThrowingUpdateSource(trace, exception);
        var dispatcher = new RecordingDispatcher(trace);

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton(trace);
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddTeleFlowShutdownTask<FirstShutdownTask>();
            });

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => application.RunAsync());

        Assert.Same(exception, thrown);
        Assert.Equal(["source:throw", "shutdown:1"], trace);
    }

    [Fact]
    public async Task ShutdownTaskFailureAfterUpdateSourceFailure_PreservesBothFailures()
    {
        var trace = new List<string>();
        var sourceException = new InvalidOperationException("source failed");
        var shutdownException = new ApplicationException("shutdown failed");
        var source = new ThrowingUpdateSource(trace, sourceException);
        var dispatcher = new RecordingDispatcher(trace);

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton(trace);
                services.AddSingleton(shutdownException);
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddTeleFlowShutdownTask<ThrowingShutdownTask>();
            });

        var thrown = await Assert.ThrowsAsync<AggregateException>(() => application.RunAsync());

        Assert.Contains(sourceException, thrown.InnerExceptions);
        Assert.Contains(shutdownException, thrown.InnerExceptions);
        Assert.Equal(["source:throw", "shutdown:throw"], trace);
    }

    [Fact]
    public async Task LifecycleTasks_CanUseScopedConstructorDependencies()
    {
        var source = new RecordingUpdateSource([]);
        var dispatcher = new RecordingDispatcher();
        var recorder = new ScopedProbeRecorder();

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton(recorder);
                services.AddScoped<ScopedProbe>();
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddTeleFlowStartupTask<ScopedStartupTask>();
                services.AddTeleFlowStartupTask<SecondScopedStartupTask>();
                services.AddTeleFlowShutdownTask<ScopedShutdownTask>();
            });

        await application.RunAsync();

        Assert.Equal(2, recorder.StartupProbeIds.Count);
        Assert.Single(recorder.ShutdownProbeIds);
        Assert.NotEqual(recorder.StartupProbeIds[0], recorder.StartupProbeIds[1]);
    }

    [Fact]
    public async Task LifecycleTasks_ReceiveRunCancellationToken()
    {
        var source = new RecordingUpdateSource([]);
        var dispatcher = new RecordingDispatcher();
        var recorder = new CancellationTokenRecorder();
        using var cancellation = new CancellationTokenSource();

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton(recorder);
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddTeleFlowStartupTask<TokenRecordingStartupTask>();
                services.AddTeleFlowShutdownTask<TokenRecordingShutdownTask>();
            });

        await application.RunAsync(cancellation.Token);

        Assert.Equal(cancellation.Token, recorder.StartupToken);
        Assert.Equal(cancellation.Token, recorder.ShutdownToken);
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
    public void Build_FailsClearlyForDirectStartupTaskServiceRegistration()
    {
        var builder = TeleFlowApplication.CreateBuilder();

        builder.Services.AddSingleton<IUpdateSource>(new RecordingUpdateSource([]));
        builder.Services.AddSingleton<IUpdateDispatcher>(new RecordingDispatcher());
        builder.Services.AddSingleton<ITeleFlowStartupTask, DirectStartupTask>();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains(nameof(ITeleFlowStartupTask), exception.Message);
        Assert.Contains(nameof(ApplicationLifecycleServiceCollectionExtensions.AddTeleFlowStartupTask), exception.Message);
    }

    [Fact]
    public void Build_FailsClearlyForDirectShutdownTaskServiceRegistration()
    {
        var builder = TeleFlowApplication.CreateBuilder();

        builder.Services.AddSingleton<IUpdateSource>(new RecordingUpdateSource([]));
        builder.Services.AddSingleton<IUpdateDispatcher>(new RecordingDispatcher());
        builder.Services.AddSingleton<ITeleFlowShutdownTask, DirectShutdownTask>();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains(nameof(ITeleFlowShutdownTask), exception.Message);
        Assert.Contains(nameof(ApplicationLifecycleServiceCollectionExtensions.AddTeleFlowShutdownTask), exception.Message);
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

    private sealed class RecordingUpdateSource(
        IReadOnlyList<IUpdatePayload> payloads,
        List<string>? trace = null) : IUpdateSource
    {
        public int StartCallCount { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public async Task StartAsync(
            Func<IUpdatePayload, CancellationToken, Task> updateHandler,
            CancellationToken cancellationToken = default)
        {
            StartCallCount++;
            ReceivedCancellationToken = cancellationToken;
            trace?.Add("source:start");

            foreach (var payload in payloads)
            {
                await updateHandler(payload, cancellationToken);
            }
        }
    }

    private sealed class ThrowingUpdateSource(
        List<string> trace,
        Exception exception) : IUpdateSource
    {
        public Task StartAsync(
            Func<IUpdatePayload, CancellationToken, Task> updateHandler,
            CancellationToken cancellationToken = default)
        {
            trace.Add("source:throw");
            return Task.FromException(exception);
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

        public List<Guid> StartupProbeIds { get; } = [];

        public List<Guid> ShutdownProbeIds { get; } = [];
    }

    private sealed class SingletonMiddlewareRecorder
    {
        public List<Guid> MiddlewareInstanceIds { get; } = [];
    }

    private sealed class ScopedProbe
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class CancellationTokenRecorder
    {
        public CancellationToken StartupToken { get; set; }

        public CancellationToken ShutdownToken { get; set; }
    }

    private sealed class FirstStartupTask(List<string> trace) : ITeleFlowStartupTask
    {
        public ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            trace.Add("startup:1");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SecondStartupTask(List<string> trace) : ITeleFlowStartupTask
    {
        public ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            trace.Add("startup:2");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingStartupTask(
        List<string> trace,
        InvalidOperationException exception) : ITeleFlowStartupTask
    {
        public ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            trace.Add("startup:throw");
            return ValueTask.FromException(exception);
        }
    }

    private sealed class FirstShutdownTask(List<string> trace) : ITeleFlowShutdownTask
    {
        public ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            trace.Add("shutdown:1");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SecondShutdownTask(List<string> trace) : ITeleFlowShutdownTask
    {
        public ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            trace.Add("shutdown:2");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingShutdownTask(
        List<string> trace,
        ApplicationException exception) : ITeleFlowShutdownTask
    {
        public ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            trace.Add("shutdown:throw");
            return ValueTask.FromException(exception);
        }
    }

    private sealed class ScopedStartupTask(
        ScopedProbe probe,
        ScopedProbeRecorder recorder) : ITeleFlowStartupTask
    {
        public ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            recorder.StartupProbeIds.Add(probe.Id);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SecondScopedStartupTask(
        ScopedProbe probe,
        ScopedProbeRecorder recorder) : ITeleFlowStartupTask
    {
        public ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            recorder.StartupProbeIds.Add(probe.Id);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ScopedShutdownTask(
        ScopedProbe probe,
        ScopedProbeRecorder recorder) : ITeleFlowShutdownTask
    {
        public ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            recorder.ShutdownProbeIds.Add(probe.Id);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TokenRecordingStartupTask(CancellationTokenRecorder recorder) : ITeleFlowStartupTask
    {
        public ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            recorder.StartupToken = cancellationToken;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TokenRecordingShutdownTask(CancellationTokenRecorder recorder) : ITeleFlowShutdownTask
    {
        public ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            recorder.ShutdownToken = cancellationToken;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DirectStartupTask : ITeleFlowStartupTask
    {
        public ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DirectShutdownTask : ITeleFlowShutdownTask
    {
        public ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }
}
