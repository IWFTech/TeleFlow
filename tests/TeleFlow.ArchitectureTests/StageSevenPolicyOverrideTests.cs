using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Annotations;
using TeleFlow.Framework.Callbacks;
using TeleFlow.Framework.DependencyInjection;
using TeleFlow.Framework.Dispatching;
using TeleFlow.Framework.Middleware;
using TeleFlow.Framework.RateLimiting;
using TeleFlow.Framework.States;
using TeleFlow.Framework.Updates;
using TeleFlow.Storage.Memory;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Abstractions;

namespace TeleFlow.ArchitectureTests;

public sealed class StageSevenPolicyOverrideTests
{
    [Fact]
    public void AddTelegramBot_DoesNotReplaceCustomPolicyDefaults()
    {
        using var httpClient = new HttpClient();
        var telegramTransport = new CustomTelegramTransport();
        var telegramClient = new CustomTelegramClient();
        var executor = new CustomTelegramRequestExecutor();
        var serializer = new CustomCallbackDataSerializer();
        var stateKeyFactory = new CustomStateKeyFactory();
        var services = new ServiceCollection();

        services.AddSingleton(httpClient);
        services.AddSingleton<ITelegramTransport>(telegramTransport);
        services.AddSingleton<ITelegramClient>(telegramClient);
        services.AddSingleton<ITelegramRequestExecutor>(executor);
        services.AddSingleton<ICallbackDataSerializer>(serializer);
        services.AddSingleton<IStateKeyFactory>(stateKeyFactory);

        services.AddTelegramBot(options => options.Token = "test-token");

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.Same(httpClient, serviceProvider.GetRequiredService<HttpClient>());
        Assert.Same(telegramTransport, serviceProvider.GetRequiredService<ITelegramTransport>());
        Assert.Same(telegramClient, serviceProvider.GetRequiredService<ITelegramClient>());
        Assert.Same(executor, serviceProvider.GetRequiredService<ITelegramRequestExecutor>());
        Assert.Same(serializer, serviceProvider.GetRequiredService<ICallbackDataSerializer>());
        Assert.Same(stateKeyFactory, serviceProvider.GetRequiredService<IStateKeyFactory>());
    }

    [Fact]
    public void AddTelegramHandler_DoesNotReplaceCustomDispatcher()
    {
        var services = new ServiceCollection();
        var dispatcher = new CustomDispatcher();

        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddSingleton<IUpdateDispatcher>(dispatcher);
        services.AddTelegramHandler<NoOpMessageHandler>();

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.Same(dispatcher, serviceProvider.GetRequiredService<IUpdateDispatcher>());
        Assert.Single(serviceProvider.GetServices<IUpdateDispatcher>());
    }

    [Fact]
    public void AddLongPolling_FailsWhenUpdateSourceAlreadyRegistered()
    {
        var services = new ServiceCollection();

        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddUpdateSource<CustomUpdateSource>();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddLongPolling());

        Assert.Contains(nameof(IUpdateSource), exception.Message);
    }

    [Fact]
    public async Task CustomUpdateSourceHelper_AllowsApplicationWithoutLongPolling()
    {
        var builder = TeleFlow.Framework.Application.TeleFlowApplication.CreateBuilder();

        CustomUpdateSource.Payloads = [new TestUpdatePayload("custom")];
        CustomDispatcher.Trace.Clear();

        builder.Services.AddUpdateSource<CustomUpdateSource>();
        builder.Services.AddUpdateDispatcher<CustomDispatcher>();

        await using var application = builder.Build();
        await application.RunAsync();

        Assert.Equal(["custom"], CustomDispatcher.Trace);
    }

    [Fact]
    public void SingleServiceReplacementHelpers_ReplacePriorRegistrations()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IUpdateDispatcher, ThrowingDispatcher>();
        services.AddUpdateDispatcher<CustomDispatcher>();
        services.AddSingleton<IUpdateSource, ThrowingUpdateSource>();
        services.AddUpdateSource<CustomUpdateSource>();
        services.AddSingleton<ITelegramRequestExecutor, ThrowingTelegramRequestExecutor>();
        services.AddTelegramRequestExecutor<CustomTelegramRequestExecutor>();
        services.AddSingleton<ITelegramClient, ThrowingTelegramClient>();
        services.AddTelegramClient<CustomTelegramClient>();
        services.AddSingleton<ICallbackDataSerializer, ThrowingCallbackDataSerializer>();
        services.AddCallbackDataSerializer<CustomCallbackDataSerializer>();
        services.AddSingleton<IStateStore, ThrowingStateStore>();
        services.AddStateStore<CustomStateStore>();
        services.AddSingleton<IStateKeyFactory, ThrowingStateKeyFactory>();
        services.AddStateKeyFactory<CustomStateKeyFactory>();

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.IsType<CustomDispatcher>(serviceProvider.GetRequiredService<IUpdateDispatcher>());
        Assert.Single(serviceProvider.GetServices<IUpdateDispatcher>());
        Assert.IsType<CustomUpdateSource>(serviceProvider.GetRequiredService<IUpdateSource>());
        Assert.Single(serviceProvider.GetServices<IUpdateSource>());
        Assert.IsType<CustomTelegramRequestExecutor>(serviceProvider.GetRequiredService<ITelegramRequestExecutor>());
        Assert.Single(serviceProvider.GetServices<ITelegramRequestExecutor>());
        Assert.IsType<CustomTelegramClient>(serviceProvider.GetRequiredService<ITelegramClient>());
        Assert.Single(serviceProvider.GetServices<ITelegramClient>());
        Assert.IsType<CustomCallbackDataSerializer>(serviceProvider.GetRequiredService<ICallbackDataSerializer>());
        Assert.Single(serviceProvider.GetServices<ICallbackDataSerializer>());
        Assert.IsType<CustomStateStore>(serviceProvider.GetRequiredService<IStateStore>());
        Assert.Single(serviceProvider.GetServices<IStateStore>());
        Assert.IsType<CustomStateKeyFactory>(serviceProvider.GetRequiredService<IStateKeyFactory>());
        Assert.Single(serviceProvider.GetServices<IStateKeyFactory>());
    }

    [Fact]
    public async Task AddDefaultUpdateRateLimiting_RegistersNoOpLimiterMiddleware_AndAdditiveLimitersRunInOrder()
    {
        var trace = new List<string>();
        using var serviceProvider = new ServiceCollection()
            .AddSingleton(trace)
            .AddDefaultUpdateRateLimiting()
            .AddUpdateRateLimiter<FirstRecordingRateLimiter>()
            .AddUpdateRateLimiter<SecondRecordingRateLimiter>()
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        var limiters = serviceProvider.GetServices<IUpdateRateLimiter>().ToArray();

        Assert.Collection(
            limiters,
            limiter => Assert.IsType<NoOpUpdateRateLimiter>(limiter),
            limiter => Assert.IsType<FirstRecordingRateLimiter>(limiter),
            limiter => Assert.IsType<SecondRecordingRateLimiter>(limiter));
        Assert.Contains(
            serviceProvider.GetServices<UpdateMiddlewareRegistration>(),
            registration => registration.MiddlewareType == typeof(UpdateRateLimitMiddleware));

        using var scope = serviceProvider.CreateScope();
        var context = new UpdateContext(scope.ServiceProvider, new TestUpdatePayload("rate-limit"));
        var middleware = scope.ServiceProvider.GetRequiredService<UpdateRateLimitMiddleware>();

        await middleware.InvokeAsync(
            context,
            _ =>
            {
                trace.Add("next");
                return Task.CompletedTask;
            });

        Assert.Equal(["first", "second", "next"], trace);
    }

    [Fact]
    public void AddMemoryStateStorage_DoesNotReplaceCustomStateStore()
    {
        var customStore = new CustomStateStore();
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IStateStore>(customStore)
            .AddSingleton<IStateKeyFactory, CustomStateKeyFactory>()
            .AddMemoryStateStorage()
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        Assert.Same(customStore, serviceProvider.GetRequiredService<IStateStore>());
    }

    public sealed class NoOpMessageHandler
    {
        [Message]
        public Task Handle(MessageContext context)
        {
            return Task.CompletedTask;
        }
    }

    private sealed record TestUpdatePayload(string Name) : IUpdatePayload;

    private sealed class CustomUpdateSource : IUpdateSource
    {
        public static IReadOnlyList<IUpdatePayload> Payloads { get; set; } = [];

        public async Task StartAsync(
            Func<IUpdatePayload, CancellationToken, Task> updateHandler,
            CancellationToken cancellationToken = default)
        {
            foreach (var payload in Payloads)
            {
                await updateHandler(payload, cancellationToken);
            }
        }
    }

    private sealed class ThrowingUpdateSource : IUpdateSource
    {
        public Task StartAsync(
            Func<IUpdatePayload, CancellationToken, Task> updateHandler,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("This update source should have been replaced.");
        }
    }

    private sealed class CustomDispatcher : IUpdateDispatcher
    {
        public static List<string> Trace { get; } = [];

        public Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            Trace.Add(((TestUpdatePayload)context.Payload).Name);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDispatcher : IUpdateDispatcher
    {
        public Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("This dispatcher should have been replaced.");
        }
    }

    private sealed class CustomTelegramClient : ITelegramClient
    {
        public TelegramBotDefaults Defaults { get; } = new();

        public TelegramDeepLinks DeepLinks { get; } =
            new("test_bot", new Base64UrlJsonDeepLinkPayloadSerializer());

        public Task<TResult> SendAsync<TResult>(
            ITelegramApiMethod<TResult> method,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("The test client should not send Telegram requests.");
        }
    }

    private sealed class CustomTelegramTransport : ITelegramTransport
    {
        public Task<TelegramTransportResponse> SendAsync(
            TelegramTransportRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("The test transport should not send Telegram requests.");
        }
    }

    private sealed class ThrowingTelegramClient : ITelegramClient
    {
        public TelegramBotDefaults Defaults { get; } = new();

        public TelegramDeepLinks DeepLinks { get; } =
            new("test_bot", new Base64UrlJsonDeepLinkPayloadSerializer());

        public Task<TResult> SendAsync<TResult>(
            ITelegramApiMethod<TResult> method,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("This Telegram client should have been replaced.");
        }
    }

    private sealed class CustomTelegramRequestExecutor : ITelegramRequestExecutor
    {
        public Task<TResponse> ExecuteAsync<TResponse>(
            ITelegramRequest<TResponse> request,
            CancellationToken cancellationToken = default)
            where TResponse : ITelegramResponse
        {
            throw new InvalidOperationException("The test executor should not execute Telegram requests.");
        }
    }

    private sealed class ThrowingTelegramRequestExecutor : ITelegramRequestExecutor
    {
        public Task<TResponse> ExecuteAsync<TResponse>(
            ITelegramRequest<TResponse> request,
            CancellationToken cancellationToken = default)
            where TResponse : ITelegramResponse
        {
            throw new InvalidOperationException("This executor should have been replaced.");
        }
    }

    private sealed class CustomCallbackDataSerializer : ICallbackDataSerializer
    {
        public string Serialize<TPayload>(TPayload payload)
        {
            return "custom";
        }

        public TPayload Deserialize<TPayload>(string serializedPayload)
        {
            throw new InvalidOperationException("The test serializer should not deserialize callback data.");
        }
    }

    private sealed class ThrowingCallbackDataSerializer : ICallbackDataSerializer
    {
        public string Serialize<TPayload>(TPayload payload)
        {
            throw new InvalidOperationException("This serializer should have been replaced.");
        }

        public TPayload Deserialize<TPayload>(string serializedPayload)
        {
            throw new InvalidOperationException("This serializer should have been replaced.");
        }
    }

    private sealed class CustomStateStore : IStateStore
    {
        public ValueTask<string?> GetStateAsync(StateKey key, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public ValueTask SetStateAsync(StateKey key, string state, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearStateAsync(StateKey key, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingStateStore : IStateStore
    {
        public ValueTask<string?> GetStateAsync(StateKey key, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("This state store should have been replaced.");
        }

        public ValueTask SetStateAsync(StateKey key, string state, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("This state store should have been replaced.");
        }

        public ValueTask ClearStateAsync(StateKey key, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("This state store should have been replaced.");
        }
    }

    private sealed class CustomStateKeyFactory : IStateKeyFactory
    {
        public bool TryCreateStateKey(UpdateContext context, out StateKey key)
        {
            key = StateKey.Create("custom", "custom");
            return true;
        }
    }

    private sealed class ThrowingStateKeyFactory : IStateKeyFactory
    {
        public bool TryCreateStateKey(UpdateContext context, out StateKey key)
        {
            throw new InvalidOperationException("This state key factory should have been replaced.");
        }
    }

    private sealed class FirstRecordingRateLimiter(List<string> trace) : IUpdateRateLimiter
    {
        public ValueTask WaitAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            trace.Add("first");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SecondRecordingRateLimiter(List<string> trace) : IUpdateRateLimiter
    {
        public ValueTask WaitAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            trace.Add("second");
            return ValueTask.CompletedTask;
        }
    }
}
