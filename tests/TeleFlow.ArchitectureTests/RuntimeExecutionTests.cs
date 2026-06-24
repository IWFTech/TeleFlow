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
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddSingleton<IUpdateMiddleware>(new ProbeMiddleware("mw-1", trace));
                services.AddSingleton<IUpdateMiddleware>(new ProbeMiddleware("mw-2", trace));
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
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddSingleton<IUpdateMiddleware>(new ProbeMiddleware("stop", trace, shortCircuit: true));
            });

        await application.RunAsync();

        Assert.Equal(0, dispatcher.InvocationCount);
        Assert.Equal(["enter:stop", "short-circuit:stop"], trace);
    }

    [Fact]
    public async Task Dispatcher_ReceivesSameContextSeenByMiddleware()
    {
        var source = new RecordingUpdateSource([new TestUpdatePayload("only")]);
        var dispatcher = new RecordingDispatcher();
        UpdateContext? middlewareContext = null;

        var application = CreateApplication(
            services =>
            {
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddSingleton<IUpdateMiddleware>(
                    new DelegateMiddleware((context, next) =>
                    {
                        middlewareContext = context;
                        return next(context);
                    }));
            });

        await application.RunAsync();

        Assert.Same(middlewareContext, dispatcher.Contexts.Single());
    }

    [Fact]
    public async Task EachUpdate_GetsDistinctScopedServiceInstance()
    {
        var source = new RecordingUpdateSource(
        [
            new TestUpdatePayload("first"),
            new TestUpdatePayload("second")
        ]);
        var dispatcher = new RecordingDispatcher();

        var application = CreateApplication(
            services =>
            {
                services.AddScoped<ScopedProbe>();
                services.AddSingleton<IUpdateSource>(source);
                services.AddSingleton<IUpdateDispatcher>(dispatcher);
                services.AddSingleton<IUpdateMiddleware>(
                    new DelegateMiddleware((context, next) =>
                    {
                        var probeFromFirstResolution = context.Services.GetRequiredService<ScopedProbe>();
                        var probeFromSecondResolution = context.Services.GetRequiredService<ScopedProbe>();

                        context.Items["scoped-first"] = probeFromFirstResolution.Id;
                        context.Items["scoped-second"] = probeFromSecondResolution.Id;

                        return next(context);
                    }));
            });

        await application.RunAsync();

        var firstContext = dispatcher.Contexts[0];
        var secondContext = dispatcher.Contexts[1];

        Assert.Equal(firstContext.Items["scoped-first"], firstContext.Items["scoped-second"]);
        Assert.Equal(secondContext.Items["scoped-first"], secondContext.Items["scoped-second"]);
        Assert.NotEqual(firstContext.Items["scoped-first"], secondContext.Items["scoped-first"]);
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
                services.AddSingleton<IUpdateMiddleware>(new ThrowingMiddleware(exception));
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

        services.AddSingleton<IUpdateDispatcher>(dispatcher);
        services.AddSingleton<IUpdateMiddleware>(new ProbeMiddleware("mw-1", trace));
        services.AddSingleton<IUpdateMiddleware>(new ProbeMiddleware("mw-2", trace));
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

        services.AddScoped<ScopedProbe>();
        services.AddSingleton<IUpdateDispatcher>(dispatcher);
        services.AddSingleton<IUpdateMiddleware>(
            new DelegateMiddleware((context, next) =>
            {
                context.Items["scope"] = context.Services.GetRequiredService<ScopedProbe>().Id;
                return next(context);
            }));
        services.AddSingleton<IUpdateProcessor, DefaultUpdateProcessor>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var processor = provider.GetRequiredService<IUpdateProcessor>();

        await processor.ProcessAsync(new TestUpdatePayload("first"));
        await processor.ProcessAsync(new TestUpdatePayload("second"));

        Assert.NotEqual(dispatcher.Contexts[0].Items["scope"], dispatcher.Contexts[1].Items["scope"]);
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

    private sealed class ProbeMiddleware : IUpdateMiddleware
    {
        private readonly string _name;
        private readonly List<string> _trace;
        private readonly bool _shortCircuit;

        public ProbeMiddleware(string name, List<string> trace, bool shortCircuit = false)
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

    private sealed class DelegateMiddleware : IUpdateMiddleware
    {
        private readonly Func<UpdateContext, UpdateDelegate, Task> _handler;

        public DelegateMiddleware(Func<UpdateContext, UpdateDelegate, Task> handler)
        {
            _handler = handler;
        }

        public Task InvokeAsync(UpdateContext context, UpdateDelegate next)
        {
            return _handler(context, next);
        }
    }

    private sealed class ThrowingMiddleware(Exception exception) : IUpdateMiddleware
    {
        public Task InvokeAsync(UpdateContext context, UpdateDelegate next)
        {
            return Task.FromException(exception);
        }
    }

    private sealed class ScopedProbe
    {
        public Guid Id { get; } = Guid.NewGuid();
    }
}
