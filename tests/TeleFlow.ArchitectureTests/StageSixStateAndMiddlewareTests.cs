using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeleFlow.Annotations;
using TeleFlow.Core.Application;
using TeleFlow.Core.DependencyInjection;
using TeleFlow.Core.Dispatching;
using TeleFlow.Core.Middleware;
using TeleFlow.Core.RateLimiting;
using TeleFlow.Core.States;
using TeleFlow.Core.Updates;
using TeleFlow.Storage.Memory;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.ArchitectureTests;

public sealed class StageSixStateAndMiddlewareTests
{
    private const string RegistrationNameState = "registration:name";
    private const string RegistrationAgeState = "registration:age";
    private const string CallbackConfirmState = "callback:confirm";

    [Fact]
    public async Task MemoryStateStore_SetsGetsClears_AndIsolatesKeys()
    {
        var store = new MemoryStateStore();
        var firstKey = StateKey.Create("telegram", "user:1", "chat:10");
        var secondKey = StateKey.Create("telegram", "user:1", "chat:20");

        await store.SetStateAsync(firstKey, "first");
        await store.SetStateAsync(secondKey, "second");

        Assert.Equal("first", await store.GetStateAsync(firstKey));
        Assert.Equal("second", await store.GetStateAsync(secondKey));

        await store.ClearStateAsync(firstKey);

        Assert.Null(await store.GetStateAsync(firstKey));
        Assert.Equal("second", await store.GetStateAsync(secondKey));
    }

    [Fact]
    public async Task MemoryStateDataStore_SetsGetsRemovesClears_AndIsolatesKeys()
    {
        var store = new MemoryStateDataStore();
        var firstKey = StateKey.Create("telegram", "user:1", "chat:10");
        var secondKey = StateKey.Create("telegram", "user:1", "chat:20");

        await store.SetDataAsync(firstKey, "name", "\"Alice\"");
        await store.SetDataAsync(firstKey, "age", "42");
        await store.SetDataAsync(firstKey, "empty", string.Empty);
        await store.SetDataAsync(secondKey, "name", "\"Bob\"");

        Assert.Equal("\"Alice\"", await store.GetDataAsync(firstKey, "name"));
        Assert.Equal("42", await store.GetDataAsync(firstKey, "age"));
        Assert.Equal(string.Empty, await store.GetDataAsync(firstKey, "empty"));
        Assert.Equal("\"Bob\"", await store.GetDataAsync(secondKey, "name"));

        await store.RemoveDataAsync(firstKey, "name");

        Assert.Null(await store.GetDataAsync(firstKey, "name"));
        Assert.Equal("42", await store.GetDataAsync(firstKey, "age"));
        Assert.Equal("\"Bob\"", await store.GetDataAsync(secondKey, "name"));

        await store.ClearDataAsync(firstKey);

        Assert.Null(await store.GetDataAsync(firstKey, "age"));
        Assert.Equal("\"Bob\"", await store.GetDataAsync(secondKey, "name"));
    }

    [Fact]
    public async Task MemoryStateHistoryStore_PushesPopsClears_AndIsolatesKeys()
    {
        var store = new MemoryStateHistoryStore();
        var firstKey = StateKey.Create("telegram", "user:1", "chat:10");
        var secondKey = StateKey.Create("telegram", "user:1", "chat:20");

        await store.PushAsync(firstKey, "first:name");
        await store.PushAsync(firstKey, "first:age");
        await store.PushAsync(secondKey, "second:name");

        Assert.Equal(["first:name", "first:age"], await store.GetHistoryAsync(firstKey));
        Assert.Equal(["second:name"], await store.GetHistoryAsync(secondKey));
        Assert.Equal("first:age", await store.PeekAsync(firstKey));
        Assert.Equal(["first:name", "first:age"], await store.GetHistoryAsync(firstKey));
        Assert.Equal("first:age", await store.PopAsync(firstKey));
        Assert.Equal("first:name", await store.PeekAsync(firstKey));
        Assert.Equal("first:name", await store.PopAsync(firstKey));
        Assert.Null(await store.PeekAsync(firstKey));
        Assert.Null(await store.PopAsync(firstKey));
        Assert.Equal(["second:name"], await store.GetHistoryAsync(secondKey));

        await store.ClearAsync(secondKey);

        Assert.Empty(await store.GetHistoryAsync(secondKey));
    }

    [Fact]
    public async Task UpdateStateData_RoundTripsTypedValues_AndHandlesMissingRequiredAndNullValues()
    {
        var state = CreateUpdateStateWithData();

        await state.Data.SetAsync("name", "Alice");
        await state.Data.SetAsync("age", 42);
        await state.Data.SetAsync("premium", true);
        await state.Data.SetAsync("profile", new StateProfile("Alice", 42));

        Assert.Equal("Alice", await state.Data.GetAsync<string>("name"));
        Assert.Equal(42, await state.Data.GetAsync<int>("age"));
        Assert.True(await state.Data.GetAsync<bool>("premium"));
        Assert.Equal(new StateProfile("Alice", 42), await state.Data.GetRequiredAsync<StateProfile>("profile"));
        Assert.Null(await state.Data.GetAsync<string>("missing"));

        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await state.Data.GetRequiredAsync<string>("missing"));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await state.Data.GetAsync<string>(" "));
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await state.Data.SetAsync<string>("null", null!));
    }

    [Fact]
    public async Task UpdateState_ClearAndReset_HaveExplicitStateDataLifecycle()
    {
        var state = CreateUpdateStateWithData();

        await state.SetAsync("awaiting-name");
        await state.Data.SetAsync("name", "Alice");

        await state.ClearAsync();

        Assert.Null(await state.GetAsync());
        Assert.Equal("Alice", await state.Data.GetRequiredAsync<string>("name"));

        await state.SetAsync("awaiting-age");
        await state.ResetAsync();

        Assert.Null(await state.GetAsync());
        Assert.Null(await state.Data.GetAsync<string>("name"));
    }

    [Fact]
    public async Task UpdateWizard_GoToBackReset_ManageStateHistoryAndDataExplicitly()
    {
        var stateStore = new MemoryStateStore();
        var dataStore = new MemoryStateDataStore();
        var historyStore = new MemoryStateHistoryStore();
        var key = StateKey.Create("telegram", "user:1", "chat:10");
        var state = new UpdateState(
            stateStore,
            key,
            dataStore,
            new JsonStateDataSerializer(),
            historyStore);

        await state.SetAsync(State.Create("registration:name"));
        await state.Data.SetAsync("name", "Alice");
        await state.Wizard.GoToAsync(State.Create("registration:age"));

        Assert.Equal(State.Create("registration:age"), state.Wizard.Current);
        Assert.Equal("registration:age", await state.GetAsync());
        Assert.Equal(["registration:name"], await historyStore.GetHistoryAsync(key));
        Assert.Equal("Alice", await state.Data.GetRequiredAsync<string>("name"));

        await state.Wizard.BackAsync();

        Assert.Equal(State.Create("registration:name"), state.Wizard.Current);
        Assert.Equal("registration:name", await state.GetAsync());
        Assert.Empty(await historyStore.GetHistoryAsync(key));
        Assert.Equal("Alice", await state.Data.GetRequiredAsync<string>("name"));

        await state.Wizard.GoToAsync(State.Create("registration:age"));
        await state.Wizard.ResetAsync();

        Assert.Null(await state.GetAsync());
        Assert.Null(await state.Data.GetAsync<string>("name"));
        Assert.Empty(await historyStore.GetHistoryAsync(key));
    }

    [Fact]
    public async Task UpdateWizard_GoToWithoutCurrentState_DoesNotPushHistory()
    {
        var historyStore = new MemoryStateHistoryStore();
        var key = StateKey.Create("telegram", "user:1", "chat:10");
        var state = new UpdateState(
            new MemoryStateStore(),
            key,
            new MemoryStateDataStore(),
            new JsonStateDataSerializer(),
            historyStore);

        Assert.Throws<InvalidOperationException>(() => state.Wizard.Current);

        await state.Wizard.GoToAsync(State.Create("registration:name"));

        Assert.Equal(State.Create("registration:name"), state.Wizard.Current);
        Assert.Empty(await historyStore.GetHistoryAsync(key));
    }

    [Fact]
    public async Task UpdateWizard_GoToAsync_DoesNotPushHistoryWhenStateSetFails()
    {
        var key = StateKey.Create("telegram", "user:1", "chat:10");
        var historyStore = new MemoryStateHistoryStore();
        var state = new UpdateState(
            new ThrowingSetStateStore(),
            key,
            new MemoryStateDataStore(),
            new JsonStateDataSerializer(),
            historyStore);

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await state.Wizard.GoToAsync(State.Create(RegistrationAgeState)));

        Assert.Empty(await historyStore.GetHistoryAsync(key));
    }

    [Fact]
    public async Task UpdateWizard_GoToAsync_PushFailureKeepsNextStateActive()
    {
        var key = StateKey.Create("telegram", "user:1", "chat:10");
        var stateStore = new MemoryStateStore();
        var state = new UpdateState(
            stateStore,
            key,
            new MemoryStateDataStore(),
            new JsonStateDataSerializer(),
            new ThrowingPushStateHistoryStore());

        await state.SetAsync(RegistrationNameState);

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await state.Wizard.GoToAsync(State.Create(RegistrationAgeState)));

        Assert.Equal(RegistrationAgeState, await stateStore.GetStateAsync(key));
    }

    [Fact]
    public async Task UpdateWizard_GetCurrentAsync_ReadsCurrentStateAndUpdatesSnapshot()
    {
        var stateStore = new MemoryStateStore();
        var key = StateKey.Create("telegram", "user:1", "chat:10");
        var state = new UpdateState(
            stateStore,
            key,
            new MemoryStateDataStore(),
            new JsonStateDataSerializer(),
            new MemoryStateHistoryStore());

        await stateStore.SetStateAsync(key, RegistrationNameState);

        Assert.Throws<InvalidOperationException>(() => state.Wizard.Current);
        Assert.Equal(State.Create(RegistrationNameState), await state.Wizard.GetCurrentAsync());
        Assert.Equal(State.Create(RegistrationNameState), state.Wizard.Current);
    }

    [Fact]
    public async Task UpdateWizard_BackWithoutHistory_FailsClearly()
    {
        var state = CreateUpdateStateWithWizard();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await state.Wizard.BackAsync());

        Assert.Contains("state history is empty", exception.Message);
    }

    [Fact]
    public async Task UpdateWizard_BackAsync_DoesNotPopHistoryWhenStateSetFails()
    {
        var key = StateKey.Create("telegram", "user:1", "chat:10");
        var historyStore = new MemoryStateHistoryStore();
        var state = new UpdateState(
            new ThrowingSetStateStore(),
            key,
            new MemoryStateDataStore(),
            new JsonStateDataSerializer(),
            historyStore);

        await historyStore.PushAsync(key, RegistrationNameState);

        await Assert.ThrowsAsync<NotSupportedException>(async () => await state.Wizard.BackAsync());

        Assert.Equal([RegistrationNameState], await historyStore.GetHistoryAsync(key));
    }

    [Fact]
    public async Task UpdateState_ClearAsync_DoesNotClearWizardHistory()
    {
        var historyStore = new MemoryStateHistoryStore();
        var key = StateKey.Create("telegram", "user:1", "chat:10");
        var state = new UpdateState(
            new MemoryStateStore(),
            key,
            new MemoryStateDataStore(),
            new JsonStateDataSerializer(),
            historyStore);

        await state.SetAsync("registration:name");
        await state.Wizard.GoToAsync(State.Create("registration:age"));
        await state.ClearAsync();

        Assert.Null(await state.GetAsync());
        Assert.Equal(["registration:name"], await historyStore.GetHistoryAsync(key));
    }

    [Fact]
    public async Task UpdateState_Reset_FailsClearlyWhenStateDataIsNotConfigured()
    {
        var state = new UpdateState(
            new MemoryStateStore(),
            StateKey.Create("telegram", "user:1", "chat:10"));

        await state.SetAsync("awaiting-name");

        var exception = Assert.Throws<InvalidOperationException>(() => state.Data);
        Assert.Contains("State data is not available", exception.Message);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await state.ResetAsync());
        Assert.Equal("awaiting-name", await state.GetAsync());
    }

    [Fact]
    public async Task UpdateState_ResetAsync_KeepsCurrentStateWhenDataClearFails()
    {
        var key = StateKey.Create("telegram", "user:1", "chat:10");
        var stateStore = new MemoryStateStore();
        var state = new UpdateState(
            stateStore,
            key,
            new ThrowingStateDataStore(),
            new JsonStateDataSerializer());

        await state.SetAsync(RegistrationAgeState);

        await Assert.ThrowsAsync<NotSupportedException>(async () => await state.ResetAsync());

        Assert.Equal(RegistrationAgeState, await stateStore.GetStateAsync(key));
    }

    [Fact]
    public async Task UpdateWizard_ResetAsync_KeepsStateWhenHistoryClearFails()
    {
        var key = StateKey.Create("telegram", "user:1", "chat:10");
        var stateStore = new MemoryStateStore();
        var dataStore = new MemoryStateDataStore();
        var state = new UpdateState(
            stateStore,
            key,
            dataStore,
            new JsonStateDataSerializer(),
            new ThrowingClearStateHistoryStore());

        await state.SetAsync(RegistrationAgeState);
        await state.Data.SetAsync("name", "Alice");

        await Assert.ThrowsAsync<NotSupportedException>(async () => await state.Wizard.ResetAsync());

        Assert.Equal(RegistrationAgeState, await stateStore.GetStateAsync(key));
        Assert.Null(await dataStore.GetDataAsync(key, "name"));
    }

    [Fact]
    public async Task UpdateWizard_ResetAsync_KeepsStateAndHistoryWhenDataClearFails()
    {
        var key = StateKey.Create("telegram", "user:1", "chat:10");
        var stateStore = new MemoryStateStore();
        var historyStore = new MemoryStateHistoryStore();
        var state = new UpdateState(
            stateStore,
            key,
            new ThrowingStateDataStore(),
            new JsonStateDataSerializer(),
            historyStore);

        await state.SetAsync(RegistrationAgeState);
        await historyStore.PushAsync(key, RegistrationNameState);

        await Assert.ThrowsAsync<NotSupportedException>(async () => await state.Wizard.ResetAsync());

        Assert.Equal(RegistrationAgeState, await stateStore.GetStateAsync(key));
        Assert.Equal([RegistrationNameState], await historyStore.GetHistoryAsync(key));
    }

    [Fact]
    public async Task UpdateStateMiddleware_DoesNotReadCurrentStateWhenCreatingUpdateState()
    {
        var key = StateKey.Create("telegram", "user:1", "chat:10");
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IStateStore, ThrowingGetStateStore>()
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        var context = new UpdateContext(serviceProvider, new TestUpdatePayload("state"));
        var middleware = new UpdateStateMiddleware(new StaticStateKeyFactory(key));

        await middleware.InvokeAsync(context, static _ => Task.CompletedTask);

        Assert.True(context.TryGetState(out var state));
        Assert.Equal(key, state.Key);
    }

    [Fact]
    public void AddMemoryStateStorage_RegistersStoreDataStoreSerializerAndStateMiddleware_WithoutReplacingCustomServices()
    {
        var services = new ServiceCollection();
        var customStore = new RecordingStateStore();
        var customDataStore = new RecordingStateDataStore();
        var customSerializer = new RecordingStateDataSerializer();
        var customHistoryStore = new RecordingStateHistoryStore();

        services.AddSingleton<IStateStore>(customStore);
        services.AddSingleton<IStateDataStore>(customDataStore);
        services.AddSingleton<IStateDataSerializer>(customSerializer);
        services.AddSingleton<IStateHistoryStore>(customHistoryStore);
        services.AddSingleton<IStateKeyFactory>(new StaticStateKeyFactory(StateKey.Create("scope", "subject")));
        services.AddMemoryStateStorage();

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.Same(customStore, serviceProvider.GetRequiredService<IStateStore>());
        Assert.Same(customDataStore, serviceProvider.GetRequiredService<IStateDataStore>());
        Assert.Same(customSerializer, serviceProvider.GetRequiredService<IStateDataSerializer>());
        Assert.Same(customHistoryStore, serviceProvider.GetRequiredService<IStateHistoryStore>());
        Assert.Contains(
            serviceProvider.GetServices<IUpdateMiddleware>(),
            middleware => middleware is UpdateStateMiddleware);
    }

    [Fact]
    public void StateDataPolicyHelpers_ReplaceExistingDataStoreAndSerializer()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IStateDataStore, ThrowingStateDataStore>();
        services.AddStateDataStore<RecordingStateDataStore>();
        services.AddSingleton<IStateDataSerializer, ThrowingStateDataSerializer>();
        services.AddStateDataSerializer<RecordingStateDataSerializer>();

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.IsType<RecordingStateDataStore>(serviceProvider.GetRequiredService<IStateDataStore>());
        Assert.Single(serviceProvider.GetServices<IStateDataStore>());
        Assert.IsType<RecordingStateDataSerializer>(serviceProvider.GetRequiredService<IStateDataSerializer>());
        Assert.Single(serviceProvider.GetServices<IStateDataSerializer>());
    }

    [Fact]
    public void StateHistoryPolicyHelper_ReplacesExistingHistoryStore()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IStateHistoryStore, ThrowingStateHistoryStore>();
        services.AddStateHistoryStore<RecordingStateHistoryStore>();

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.IsType<RecordingStateHistoryStore>(serviceProvider.GetRequiredService<IStateHistoryStore>());
        Assert.Single(serviceProvider.GetServices<IStateHistoryStore>());
    }

    [Fact]
    public async Task TelegramContext_State_IsAvailableOnlyAfterStateMiddleware()
    {
        using var serviceProvider = CreateTelegramServiceProvider(static services => services.AddMemoryStateStorage());
        using var scope = serviceProvider.CreateScope();
        var context = new UpdateContext(
            scope.ServiceProvider,
            new TelegramUpdatePayload(CreateMessageUpdate("hello", userId: 5, chatId: 100)));

        Assert.Throws<InvalidOperationException>(() => context.GetMessageContext().State);

        var stateMiddleware = serviceProvider
            .GetServices<IUpdateMiddleware>()
            .OfType<UpdateStateMiddleware>()
            .Single();

        await stateMiddleware.InvokeAsync(context, static _ => Task.CompletedTask);

        Assert.Equal(
            StateKey.Create("telegram", "user:5", "chat:100"),
            context.GetMessageContext().State.Key);
        Assert.Equal(
            StateKey.Create("telegram", "user:5", "chat:100"),
            context.GetMessageContext().State.Data.Key);
    }

    [Fact]
    public async Task TelegramContext_StateData_FailsClearlyWhenDataServicesAreMissing()
    {
        using var serviceProvider = CreateTelegramServiceProvider(
            static services =>
            {
                services.AddSingleton<IStateStore, MemoryStateStore>();
                services.AddUpdateMiddleware<UpdateStateMiddleware>();
            });
        using var scope = serviceProvider.CreateScope();
        var context = new UpdateContext(
            scope.ServiceProvider,
            new TelegramUpdatePayload(CreateMessageUpdate("hello", userId: 5, chatId: 100)));
        var stateMiddleware = serviceProvider
            .GetServices<IUpdateMiddleware>()
            .OfType<UpdateStateMiddleware>()
            .Single();

        await stateMiddleware.InvokeAsync(context, static _ => Task.CompletedTask);

        var exception = Assert.Throws<InvalidOperationException>(() => context.GetMessageContext().State.Data);
        Assert.Contains("State data is not available", exception.Message);
    }

    [Fact]
    public async Task TelegramContext_Wizard_FailsClearlyWhenHistoryStoreIsMissing()
    {
        using var serviceProvider = CreateTelegramServiceProvider(
            static services =>
            {
                services.AddSingleton<IStateStore, MemoryStateStore>();
                services.AddSingleton<IStateDataStore, MemoryStateDataStore>();
                services.AddSingleton<IStateDataSerializer, JsonStateDataSerializer>();
                services.AddUpdateMiddleware<UpdateStateMiddleware>();
            });
        using var scope = serviceProvider.CreateScope();
        var context = new UpdateContext(
            scope.ServiceProvider,
            new TelegramUpdatePayload(CreateMessageUpdate("hello", userId: 5, chatId: 100)));
        var stateMiddleware = serviceProvider
            .GetServices<IUpdateMiddleware>()
            .OfType<UpdateStateMiddleware>()
            .Single();

        await stateMiddleware.InvokeAsync(context, static _ => Task.CompletedTask);

        var exception = Assert.Throws<InvalidOperationException>(() => context.GetMessageContext().Wizard);
        Assert.Contains("Wizard is not available", exception.Message);
    }

    [Fact]
    public void TelegramStateKeyFactory_IsolatesUsersAndChats()
    {
        using var serviceProvider = CreateTelegramServiceProvider(static _ => { });
        using var scope = serviceProvider.CreateScope();
        var factory = serviceProvider.GetRequiredService<IStateKeyFactory>();

        var first = CreateStateKey(
            factory,
            scope.ServiceProvider,
            CreateMessageUpdate("first", userId: 5, chatId: 100));
        var sameUserDifferentChat = CreateStateKey(
            factory,
            scope.ServiceProvider,
            CreateMessageUpdate("second", userId: 5, chatId: 200));
        var sameChatDifferentUser = CreateStateKey(
            factory,
            scope.ServiceProvider,
            CreateMessageUpdate("third", userId: 6, chatId: 100));
        var inlineCallback = CreateStateKey(
            factory,
            scope.ServiceProvider,
            CreateCallbackUpdate("data", userId: 5, chatId: null, chatInstance: "inline-chat"));

        Assert.Equal(StateKey.Create("telegram", "user:5", "chat:100"), first);
        Assert.NotEqual(first, sameUserDifferentChat);
        Assert.NotEqual(first, sameChatDifferentUser);
        Assert.Equal(StateKey.Create("telegram", "user:5", "inline:inline-chat"), inlineCallback);
    }

    [Fact]
    public async Task StateHandlers_HavePriorityOverFallback_AndCommandsRemainFirst()
    {
        var updates = new[]
        {
            new TelegramUpdatePayload(CreateMessageUpdate("/start", userId: 5, chatId: 100)),
            new TelegramUpdatePayload(CreateMessageUpdate("/start", userId: 5, chatId: 100)),
            new TelegramUpdatePayload(CreateMessageUpdate("Alice", userId: 5, chatId: 100)),
            new TelegramUpdatePayload(CreateMessageUpdate("after", userId: 5, chatId: 100))
        };

        var probe = await RunTelegramApplicationAsync(
            updates,
            services =>
            {
                services.AddMemoryStateStorage();
                services.AddTelegramHandler<FallbackMessageHandler>();
                services.AddTelegramHandler<StateNameMessageHandler>();
                services.AddTelegramHandler<StartStateCommandHandler>();
            });

        Assert.Equal(
        [
            "start:/start",
            "start:/start",
            "state-message:Alice",
            "fallback:after"
        ], probe.Events);
    }

    [Fact]
    public async Task StateHandlers_DoNotMatchWrongState()
    {
        using var serviceProvider = CreateTelegramServiceProvider(
            services =>
            {
                services.AddSingleton<HandlerProbe>();
                services.AddMemoryStateStorage();
                services.AddTelegramHandler<WrongStateMessageHandler>();
                services.AddTelegramHandler<FallbackMessageHandler>();
            });

        await serviceProvider.GetRequiredService<IStateStore>().SetStateAsync(
            StateKey.Create("telegram", "user:5", "chat:100"),
            RegistrationNameState);

        await DispatchThroughMiddlewareAsync(
            serviceProvider,
            new TelegramUpdatePayload(CreateMessageUpdate("Alice", userId: 5, chatId: 100)));

        Assert.Equal(["fallback:Alice"], serviceProvider.GetRequiredService<HandlerProbe>().Events);
    }

    [Fact]
    public async Task CallbackStateHandlers_HavePriorityOverRawCallbackFallback()
    {
        using var serviceProvider = CreateTelegramServiceProvider(
            services =>
            {
                services.AddSingleton<HandlerProbe>();
                services.AddMemoryStateStorage();
                services.AddTelegramHandler<FallbackCallbackHandler>();
                services.AddTelegramHandler<StateCallbackHandler>();
            });

        await serviceProvider.GetRequiredService<IStateStore>().SetStateAsync(
            StateKey.Create("telegram", "user:5", "chat:100"),
            CallbackConfirmState);

        await DispatchThroughMiddlewareAsync(
            serviceProvider,
            new TelegramUpdatePayload(CreateCallbackUpdate("confirm", userId: 5, chatId: 100)));

        Assert.Equal(["state-callback:confirm"], serviceProvider.GetRequiredService<HandlerProbe>().Events);
    }

    [Fact]
    public async Task StateData_SupportsMultiStepFlowWithoutScenes()
    {
        var updates = new[]
        {
            new TelegramUpdatePayload(CreateMessageUpdate("/register", userId: 5, chatId: 100)),
            new TelegramUpdatePayload(CreateMessageUpdate("Alice", userId: 5, chatId: 100)),
            new TelegramUpdatePayload(CreateMessageUpdate("42", userId: 5, chatId: 100)),
            new TelegramUpdatePayload(CreateMessageUpdate("after", userId: 5, chatId: 100))
        };

        var probe = await RunTelegramApplicationAsync(
            updates,
            services =>
            {
                services.AddMemoryStateStorage();
                services.AddTelegramHandler<RegistrationStartHandler>();
                services.AddTelegramHandler<RegistrationNameHandler>();
                services.AddTelegramHandler<RegistrationAgeHandler>();
                services.AddTelegramHandler<FallbackMessageHandler>();
            });

        Assert.Equal(
        [
            "registration-start",
            "registration-name:Alice",
            "registration-age:Alice:42",
            "fallback:after"
        ], probe.Events);
    }

    [Fact]
    public async Task Wizard_SupportsExplicitSceneGoToBackAndReset()
    {
        using var serviceProvider = CreateTelegramServiceProvider(
            services =>
            {
                services.AddSingleton<HandlerProbe>();
                services.AddMemoryStateStorage();
                services.AddTelegramHandler<WizardRegistrationScene>();
                services.AddTelegramHandler<FallbackMessageHandler>();
            });

        await DispatchThroughMiddlewareAsync(
            serviceProvider,
            new TelegramUpdatePayload(CreateMessageUpdate("/wizard", userId: 5, chatId: 100)));
        await DispatchThroughMiddlewareAsync(
            serviceProvider,
            new TelegramUpdatePayload(CreateMessageUpdate("Alice", userId: 5, chatId: 100)));
        await DispatchThroughMiddlewareAsync(
            serviceProvider,
            new TelegramUpdatePayload(CreateMessageUpdate("back", userId: 5, chatId: 100)));
        await DispatchThroughMiddlewareAsync(
            serviceProvider,
            new TelegramUpdatePayload(CreateMessageUpdate("Bob", userId: 5, chatId: 100)));
        await DispatchThroughMiddlewareAsync(
            serviceProvider,
            new TelegramUpdatePayload(CreateMessageUpdate("42", userId: 5, chatId: 100)));
        await DispatchThroughMiddlewareAsync(
            serviceProvider,
            new TelegramUpdatePayload(CreateMessageUpdate("after", userId: 5, chatId: 100)));

        var key = StateKey.Create("telegram", "user:5", "chat:100");

        Assert.Equal(
        [
            "wizard-start",
            "wizard-name:Alice",
            "wizard-back",
            "wizard-name:Bob",
            "wizard-age:Bob:42",
            "fallback:after"
        ], serviceProvider.GetRequiredService<HandlerProbe>().Events);
        Assert.Null(await serviceProvider.GetRequiredService<IStateStore>().GetStateAsync(key));
        Assert.Null(await serviceProvider.GetRequiredService<IStateDataStore>().GetDataAsync(key, "name"));
        Assert.Empty(await serviceProvider.GetRequiredService<IStateHistoryStore>().GetHistoryAsync(key));
    }

    [Fact]
    public async Task UpdateExceptionMiddleware_LogsAndRethrowsOriginalException()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new UpdateContext(serviceProvider, new TestUpdatePayload("exception"));
        var logger = new RecordingLogger<UpdateExceptionMiddleware>();
        var middleware = new UpdateExceptionMiddleware(logger);
        var expected = new InvalidOperationException("boom");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(context, _ => Task.FromException(expected)));

        Assert.Same(expected, exception);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Error && ReferenceEquals(entry.Exception, expected));
    }

    [Fact]
    public async Task UpdateLoggingMiddleware_LogsAndDoesNotChangeExecutionResult()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new UpdateContext(serviceProvider, new TestUpdatePayload("logging"));
        var logger = new RecordingLogger<UpdateLoggingMiddleware>();
        var middleware = new UpdateLoggingMiddleware(logger);
        var called = false;

        await middleware.InvokeAsync(
            context,
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            });

        Assert.True(called);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information && entry.Message.Contains("Started"));
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information && entry.Message.Contains("Finished"));
    }

    [Fact]
    public async Task UpdateRateLimitMiddleware_InvokesRegisteredLimitersBeforeDispatcher()
    {
        var trace = new List<string>();
        var limiter = new RecordingRateLimiter(trace);

        var builder = TeleFlowApplication.CreateBuilder();
        builder.Services.AddSingleton<IUpdateSource>(
            new SequentialUpdateSource([new TestUpdatePayload("limited")]));
        builder.Services.AddSingleton<IUpdateDispatcher>(new TraceDispatcher(trace));
        builder.Services.AddSingleton<IUpdateRateLimiter>(limiter);
        builder.Services.AddUpdateMiddleware<UpdateRateLimitMiddleware>();

        await using var application = builder.Build();
        await application.RunAsync();

        Assert.Equal(["rate-limit", "dispatch"], trace);
    }

    private static ServiceProvider CreateTelegramServiceProvider(Action<IServiceCollection> configureServices)
    {
        var services = new ServiceCollection();
        services.AddTelegramBot(options => options.Token = "test-token");
        configureServices(services);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static async Task<HandlerProbe> RunTelegramApplicationAsync(
        IReadOnlyList<IUpdatePayload> updates,
        Action<IServiceCollection> configureServices)
    {
        var builder = TeleFlowApplication.CreateBuilder();
        var probe = new HandlerProbe();

        builder.Services.AddTelegramBot(options => options.Token = "test-token");
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<IUpdateSource>(new SequentialUpdateSource(updates));
        configureServices(builder.Services);

        await using var application = builder.Build();
        await application.RunAsync();

        return probe;
    }

    private static async Task DispatchThroughMiddlewareAsync(
        ServiceProvider serviceProvider,
        IUpdatePayload payload)
    {
        using var scope = serviceProvider.CreateScope();
        var context = new UpdateContext(scope.ServiceProvider, payload);
        var middleware = serviceProvider.GetServices<IUpdateMiddleware>().ToArray();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IUpdateDispatcher>();

        UpdateDelegate pipeline = updateContext => dispatcher.DispatchAsync(updateContext);

        for (var index = middleware.Length - 1; index >= 0; index--)
        {
            var current = middleware[index];
            var next = pipeline;
            pipeline = updateContext => current.InvokeAsync(updateContext, next);
        }

        await pipeline(context);
    }

    private static UpdateState CreateUpdateStateWithData()
    {
        return new UpdateState(
            new MemoryStateStore(),
            StateKey.Create("telegram", "user:1", "chat:10"),
            new MemoryStateDataStore(),
            new JsonStateDataSerializer());
    }

    private static UpdateState CreateUpdateStateWithWizard()
    {
        return new UpdateState(
            new MemoryStateStore(),
            StateKey.Create("telegram", "user:1", "chat:10"),
            new MemoryStateDataStore(),
            new JsonStateDataSerializer(),
            new MemoryStateHistoryStore());
    }

    private static StateKey CreateStateKey(
        IStateKeyFactory factory,
        IServiceProvider services,
        Update update)
    {
        var context = new UpdateContext(services, new TelegramUpdatePayload(update));

        Assert.True(factory.TryCreateStateKey(context, out var key));
        return key;
    }

    private static Update CreateMessageUpdate(string text, long userId, long chatId)
    {
        return new Update
        {
            UpdateId = 1,
            Message = new Message
            {
                MessageId = 10,
                Date = 0,
                From = new User
                {
                    Id = userId,
                    IsBot = false,
                    FirstName = $"User {userId}"
                },
                Chat = new Chat { Id = chatId, Type = "private" },
                Text = text
            }
        };
    }

    private static Update CreateCallbackUpdate(
        string data,
        long userId,
        long? chatId,
        string chatInstance = "chat-instance")
    {
        MaybeInaccessibleMessage? message = chatId is null
            ? null
            : MaybeInaccessibleMessage.From(
                new Message
                {
                    MessageId = 10,
                    Date = 0,
                    Chat = new Chat { Id = chatId.Value, Type = "private" }
                });

        return new Update
        {
            UpdateId = 1,
            CallbackQuery = new CallbackQuery
            {
                Id = "callback",
                From = new User
                {
                    Id = userId,
                    IsBot = false,
                    FirstName = $"User {userId}"
                },
                Message = message,
                ChatInstance = chatInstance,
                Data = data
            }
        };
    }

    public sealed class StartStateCommandHandler
    {
        [Command("start")]
        public async Task Handle(MessageContext context, HandlerProbe probe, CancellationToken cancellationToken)
        {
            probe.Events.Add($"start:{context.TelegramMessage.Text}");
            await context.State.SetAsync(RegistrationNameState, cancellationToken);
        }
    }

    public sealed class RegistrationStartHandler
    {
        [Command("register")]
        public async Task Handle(MessageContext context, HandlerProbe probe, CancellationToken cancellationToken)
        {
            probe.Events.Add("registration-start");
            await context.State.SetAsync(RegistrationNameState, cancellationToken);
        }
    }

    public sealed class RegistrationNameHandler
    {
        [Message]
        [State(RegistrationNameState)]
        public async Task Handle(MessageContext context, HandlerProbe probe, CancellationToken cancellationToken)
        {
            var name = context.TelegramMessage.Text ?? string.Empty;

            probe.Events.Add($"registration-name:{name}");
            await context.State.Data.SetAsync("name", name, cancellationToken);
            await context.State.SetAsync(RegistrationAgeState, cancellationToken);
        }
    }

    public sealed class RegistrationAgeHandler
    {
        [Message]
        [State(RegistrationAgeState)]
        public async Task Handle(MessageContext context, HandlerProbe probe, CancellationToken cancellationToken)
        {
            var name = await context.State.Data.GetRequiredAsync<string>("name", cancellationToken);

            probe.Events.Add($"registration-age:{name}:{context.TelegramMessage.Text}");
            await context.State.ResetAsync(cancellationToken);
        }
    }

    [Scene("wizard")]
    public sealed class WizardRegistrationScene
    {
        public static State Name => State.Create("wizard:name");

        public static State Age => State.Create("wizard:age");

        [Command("wizard")]
        public async Task Start(MessageContext context, HandlerProbe probe, CancellationToken cancellationToken)
        {
            probe.Events.Add("wizard-start");
            await context.State.SetAsync(Name, cancellationToken);
        }

        [Message]
        [SceneStep(nameof(Name))]
        public async Task NameStep(MessageContext context, HandlerProbe probe, CancellationToken cancellationToken)
        {
            var name = context.TelegramMessage.Text ?? string.Empty;

            probe.Events.Add($"wizard-name:{name}");
            await context.Wizard.Data.SetAsync("name", name, cancellationToken);
            await context.Wizard.GoToAsync(Age, cancellationToken);
        }

        [Message]
        [SceneStep(nameof(Age))]
        public async Task AgeStep(MessageContext context, HandlerProbe probe, CancellationToken cancellationToken)
        {
            if (string.Equals(context.TelegramMessage.Text, "back", StringComparison.OrdinalIgnoreCase))
            {
                probe.Events.Add("wizard-back");
                await context.Wizard.BackAsync(cancellationToken);
                return;
            }

            var name = await context.Wizard.Data.GetRequiredAsync<string>("name", cancellationToken);

            probe.Events.Add($"wizard-age:{name}:{context.TelegramMessage.Text}");
            await context.Wizard.ResetAsync(cancellationToken);
        }
    }

    public sealed class StateNameMessageHandler
    {
        [Message]
        [State(RegistrationNameState)]
        public async Task Handle(MessageContext context, HandlerProbe probe, CancellationToken cancellationToken)
        {
            probe.Events.Add($"state-message:{context.TelegramMessage.Text}");
            await context.State.ClearAsync(cancellationToken);
        }
    }

    public sealed class WrongStateMessageHandler
    {
        [Message]
        [State("wrong")]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"wrong-state:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class FallbackMessageHandler
    {
        [Message]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"fallback:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class StateCallbackHandler
    {
        [Callback]
        [State(CallbackConfirmState)]
        public Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            probe.Events.Add($"state-callback:{context.TelegramCallbackQuery.Data}");
            return Task.CompletedTask;
        }
    }

    public sealed class FallbackCallbackHandler
    {
        [Callback]
        public Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            probe.Events.Add($"fallback-callback:{context.TelegramCallbackQuery.Data}");
            return Task.CompletedTask;
        }
    }

    public sealed class HandlerProbe
    {
        public List<string> Events { get; } = [];
    }

    private sealed record TestUpdatePayload(string Name) : IUpdatePayload;

    private sealed class SequentialUpdateSource(IReadOnlyList<IUpdatePayload> payloads) : IUpdateSource
    {
        public async Task StartAsync(
            Func<IUpdatePayload, CancellationToken, Task> updateHandler,
            CancellationToken cancellationToken = default)
        {
            foreach (var payload in payloads)
            {
                await updateHandler(payload, cancellationToken);
            }
        }
    }

    private sealed class TraceDispatcher(List<string> trace) : IUpdateDispatcher
    {
        public Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            trace.Add("dispatch");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRateLimiter(List<string> trace) : IUpdateRateLimiter
    {
        public ValueTask WaitAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            trace.Add("rate-limit");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StaticStateKeyFactory(StateKey key) : IStateKeyFactory
    {
        public bool TryCreateStateKey(UpdateContext context, out StateKey stateKey)
        {
            stateKey = key;
            return true;
        }
    }

    private sealed class RecordingStateStore : IStateStore
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

    private sealed class ThrowingGetStateStore : IStateStore
    {
        public ValueTask<string?> GetStateAsync(StateKey key, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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

    private sealed class ThrowingSetStateStore : IStateStore
    {
        public ValueTask<string?> GetStateAsync(StateKey key, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(RegistrationAgeState);
        }

        public ValueTask SetStateAsync(StateKey key, string state, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask ClearStateAsync(StateKey key, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingStateDataStore : IStateDataStore
    {
        public ValueTask<string?> GetDataAsync(
            StateKey key,
            string dataKey,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public ValueTask SetDataAsync(
            StateKey key,
            string dataKey,
            string value,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveDataAsync(
            StateKey key,
            string dataKey,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearDataAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingStateDataStore : IStateDataStore
    {
        public ValueTask<string?> GetDataAsync(
            StateKey key,
            string dataKey,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask SetDataAsync(
            StateKey key,
            string dataKey,
            string value,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask RemoveDataAsync(
            StateKey key,
            string dataKey,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask ClearDataAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingStateHistoryStore : IStateHistoryStore
    {
        public ValueTask<IReadOnlyList<string>> GetHistoryAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<string>>([]);
        }

        public ValueTask PushAsync(
            StateKey key,
            string stateId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<string?> PeekAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public ValueTask<string?> PopAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public ValueTask ClearAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingStateHistoryStore : IStateHistoryStore
    {
        public ValueTask<IReadOnlyList<string>> GetHistoryAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask PushAsync(
            StateKey key,
            string stateId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<string?> PeekAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<string?> PopAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask ClearAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowingClearStateHistoryStore : IStateHistoryStore
    {
        public ValueTask<IReadOnlyList<string>> GetHistoryAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<string>>([]);
        }

        public ValueTask PushAsync(
            StateKey key,
            string stateId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<string?> PeekAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public ValueTask<string?> PopAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public ValueTask ClearAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowingPushStateHistoryStore : IStateHistoryStore
    {
        public ValueTask<IReadOnlyList<string>> GetHistoryAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<string>>([]);
        }

        public ValueTask PushAsync(
            StateKey key,
            string stateId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<string?> PeekAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public ValueTask<string?> PopAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public ValueTask ClearAsync(
            StateKey key,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingStateDataSerializer : IStateDataSerializer
    {
        public string Serialize<TValue>(TValue value)
        {
            return value?.ToString() ?? string.Empty;
        }

        public TValue? Deserialize<TValue>(string value)
        {
            return default;
        }
    }

    private sealed class ThrowingStateDataSerializer : IStateDataSerializer
    {
        public string Serialize<TValue>(TValue value)
        {
            throw new NotSupportedException();
        }

        public TValue? Deserialize<TValue>(string value)
        {
            throw new NotSupportedException();
        }
    }

    private sealed record StateProfile(string Name, int Age);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
