using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeleFlow.Annotations;
using TeleFlow.Framework.Application;
using TeleFlow.Framework.DependencyInjection;
using TeleFlow.Framework.Dispatching;
using TeleFlow.Framework.Middleware;
using TeleFlow.Framework.RateLimiting;
using TeleFlow.Framework.States;
using TeleFlow.Framework.Updates;
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
    public void UpdateState_Data_IsCreatedLazilyOnFirstAccess()
    {
        var state = CreateUpdateStateWithData();

        Assert.Null(GetUpdateStateField<UpdateStateData>(state, "_data"));

        var data = state.Data;

        Assert.Same(data, GetUpdateStateField<UpdateStateData>(state, "_data"));
    }

    [Fact]
    public void UpdateState_Wizard_IsCreatedLazilyOnFirstAccess()
    {
        var state = CreateUpdateStateWithWizard();

        Assert.Null(GetUpdateStateField<UpdateWizard>(state, "_wizard"));

        var wizard = state.Wizard;

        Assert.Same(wizard, GetUpdateStateField<UpdateWizard>(state, "_wizard"));
    }

    [Fact]
    public async Task UpdateState_GetAsync_CachesExistingStateWithinUpdate()
    {
        var store = new CountingStateStore(initialState: RegistrationNameState);
        var state = new UpdateState(store, StateKey.Create("telegram", "user:1", "chat:10"));

        Assert.Equal(RegistrationNameState, await state.GetAsync());
        Assert.Equal(RegistrationNameState, await state.GetAsync());

        Assert.Equal(1, store.GetCount);
    }

    [Fact]
    public async Task UpdateState_GetAsync_CachesMissingStateWithinUpdate()
    {
        var store = new CountingStateStore(initialState: null);
        var state = new UpdateState(store, StateKey.Create("telegram", "user:1", "chat:10"));

        Assert.Null(await state.GetAsync());
        Assert.Null(await state.GetAsync());

        Assert.Equal(1, store.GetCount);
    }

    [Fact]
    public async Task UpdateState_SetAsync_UpdatesSnapshotWithoutExtraRead()
    {
        var store = new CountingStateStore(initialState: null);
        var state = new UpdateState(store, StateKey.Create("telegram", "user:1", "chat:10"));

        await state.SetAsync(RegistrationNameState);

        Assert.Equal(RegistrationNameState, await state.GetAsync());
        Assert.Equal(0, store.GetCount);
        Assert.Equal(1, store.SetCount);
    }

    [Fact]
    public async Task UpdateState_ClearAsync_UpdatesSnapshotWithoutExtraRead()
    {
        var store = new CountingStateStore(initialState: RegistrationNameState);
        var state = new UpdateState(store, StateKey.Create("telegram", "user:1", "chat:10"));

        await state.ClearAsync();

        Assert.Null(await state.GetAsync());
        Assert.Equal(0, store.GetCount);
        Assert.Equal(1, store.ClearCount);
    }

    [Fact]
    public async Task UpdateState_IsAsync_UsesHydratedSnapshotWithoutExtraRead()
    {
        var store = new CountingStateStore(initialState: RegistrationNameState);
        var state = new UpdateState(store, StateKey.Create("telegram", "user:1", "chat:10"));

        Assert.Equal(RegistrationNameState, await state.GetAsync());

        store.ThrowOnNextGet = true;

        Assert.True(await state.IsAsync(State.Create(RegistrationNameState)));
        Assert.Equal(1, store.GetCount);
    }

    [Fact]
    public async Task UpdateState_FailedGetAsync_DoesNotCacheFailure()
    {
        var store = new CountingStateStore(initialState: RegistrationNameState)
        {
            ThrowOnNextGet = true
        };
        var state = new UpdateState(store, StateKey.Create("telegram", "user:1", "chat:10"));

        await Assert.ThrowsAsync<NotSupportedException>(async () => await state.GetAsync());

        Assert.Equal(RegistrationNameState, await state.GetAsync());
        Assert.Equal(2, store.GetCount);
    }

    [Fact]
    public async Task UpdateState_FailedSetAsync_KeepsPreviousSnapshot()
    {
        var store = new CountingStateStore(initialState: RegistrationNameState);
        var state = new UpdateState(store, StateKey.Create("telegram", "user:1", "chat:10"));

        Assert.Equal(RegistrationNameState, await state.GetAsync());

        store.ThrowOnNextSet = true;

        await Assert.ThrowsAsync<NotSupportedException>(async () => await state.SetAsync(RegistrationAgeState));

        Assert.Equal(RegistrationNameState, await state.GetAsync());
        Assert.Equal(1, store.GetCount);
        Assert.Equal(1, store.SetCount);
    }

    [Fact]
    public async Task UpdateState_FailedClearAsync_KeepsPreviousSnapshot()
    {
        var store = new CountingStateStore(initialState: RegistrationNameState);
        var state = new UpdateState(store, StateKey.Create("telegram", "user:1", "chat:10"));

        Assert.Equal(RegistrationNameState, await state.GetAsync());

        store.ThrowOnNextClear = true;

        await Assert.ThrowsAsync<NotSupportedException>(async () => await state.ClearAsync());

        Assert.Equal(RegistrationNameState, await state.GetAsync());
        Assert.Equal(1, store.GetCount);
        Assert.Equal(1, store.ClearCount);
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
    public void UpdateWizard_Current_FailsClearlyBeforeCurrentStateIsHydrated()
    {
        var state = CreateUpdateStateWithWizard();

        var exception = Assert.Throws<InvalidOperationException>(() => state.Wizard.Current);

        Assert.Contains("not hydrated", exception.Message);
    }

    [Fact]
    public async Task UpdateWizard_Current_FailsClearlyWhenHydratedStateIsMissing()
    {
        var state = CreateUpdateStateWithWizard();

        Assert.Null(await state.Wizard.GetCurrentAsync());

        var exception = Assert.Throws<InvalidOperationException>(() => state.Wizard.Current);

        Assert.Contains("no active state", exception.Message);
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
        var customKeyBuilder = new RecordingStateStorageKeyBuilder();

        services.AddSingleton<IStateStore>(customStore);
        services.AddSingleton<IStateDataStore>(customDataStore);
        services.AddSingleton<IStateDataSerializer>(customSerializer);
        services.AddSingleton<IStateHistoryStore>(customHistoryStore);
        services.AddSingleton<IStateStorageKeyBuilder>(customKeyBuilder);
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
        Assert.Same(customKeyBuilder, serviceProvider.GetRequiredService<IStateStorageKeyBuilder>());
        Assert.Contains(
            serviceProvider.GetServices<UpdateMiddlewareRegistration>(),
            registration => registration.MiddlewareType == typeof(UpdateStateMiddleware));
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

        var stateMiddleware = scope.ServiceProvider.GetRequiredService<UpdateStateMiddleware>();

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
        var stateMiddleware = scope.ServiceProvider.GetRequiredService<UpdateStateMiddleware>();

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
        var stateMiddleware = scope.ServiceProvider.GetRequiredService<UpdateStateMiddleware>();

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
    public void TelegramStateKeyFactory_IncludesBotThreadAndBusinessIsolationWhenAvailable()
    {
        using var serviceProvider = CreateTelegramServiceProvider(
            static _ => { },
            token: "777:secret");
        using var scope = serviceProvider.CreateScope();
        var factory = serviceProvider.GetRequiredService<IStateKeyFactory>();

        var key = CreateStateKey(
            factory,
            scope.ServiceProvider,
            CreateMessageUpdate(
                "business",
                userId: 5,
                chatId: 100,
                messageThreadId: 45,
                businessConnectionId: "bc-1"));

        Assert.Equal(
            new StateKey(
                StateKeyDefaults.DefaultNamespace,
                "telegram",
                "user:5",
                "bot:777:business:bc-1:chat:100:thread:45",
                StateKeyDefaults.DefaultDestiny),
            key);
    }

    [Fact]
    public void TelegramStateKeyFactory_IsolatesInlineCallbacksByBotAndChatInstance()
    {
        using var serviceProvider = CreateTelegramServiceProvider(
            static _ => { },
            token: "777:secret");
        using var scope = serviceProvider.CreateScope();
        var factory = serviceProvider.GetRequiredService<IStateKeyFactory>();

        var key = CreateStateKey(
            factory,
            scope.ServiceProvider,
            CreateCallbackUpdate("data", userId: 5, chatId: null, chatInstance: "inline-chat"));

        Assert.Equal(
            new StateKey(
                StateKeyDefaults.DefaultNamespace,
                "telegram",
                "user:5",
                "bot:777:inline:inline-chat",
                StateKeyDefaults.DefaultDestiny),
            key);
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
    public async Task UpdateExceptionMiddleware_DoesNotLogExpectedCancellation()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        var context = new UpdateContext(
            serviceProvider,
            new TestUpdatePayload("cancellation"),
            cancellation.Token);
        var logger = new RecordingLogger<UpdateExceptionMiddleware>();
        var middleware = new UpdateExceptionMiddleware(logger);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            middleware.InvokeAsync(context, _ => Task.FromCanceled(cancellation.Token)));

        Assert.Empty(logger.Entries);
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

    [Fact]
    public async Task UpdateRateLimitMiddleware_StopsPipelineAndLogsWarningWhenLimiterRejects()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var logger = new RecordingLogger<UpdateRateLimitMiddleware>();
        var middleware = new UpdateRateLimitMiddleware(
            [new RejectingRateLimiter()],
            logger);
        var context = new UpdateContext(serviceProvider, new TestUpdatePayload("secret-user-input"));
        var called = false;

        await middleware.InvokeAsync(
            context,
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            });

        Assert.False(called);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(1, entry.EventId.Id);
        Assert.Contains("Update rejected by rate limiter", entry.Message, StringComparison.Ordinal);
        Assert.Contains("policy=per-user-command", entry.Message, StringComparison.Ordinal);
        Assert.Contains("retry_after=00:00:15", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-user-input", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateRateLimitDecision_DefaultValueIsAccepted()
    {
        var decision = default(UpdateRateLimitDecision);

        Assert.True(decision.IsAccepted);
        Assert.False(decision.IsRejected);
    }

    [Fact]
    public void UpdateRateLimitDecision_RejectsNegativeRetryAfter()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => UpdateRateLimitDecision.Rejected(TimeSpan.FromMilliseconds(-1)));
    }

    private static ServiceProvider CreateTelegramServiceProvider(
        Action<IServiceCollection> configureServices,
        string token = "test-token")
    {
        var services = new ServiceCollection();
        services.AddTelegramBot(options => options.Token = token);
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
        var processor = new DefaultUpdateProcessor(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            serviceProvider.GetRequiredService<IUpdateDispatcher>(),
            serviceProvider.GetServices<UpdateMiddlewareRegistration>());

        await processor.ProcessAsync(payload);
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

    private static T? GetUpdateStateField<T>(
        UpdateState state,
        string fieldName)
        where T : class
    {
        var field = typeof(UpdateState).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (field is null)
        {
            throw new InvalidOperationException($"UpdateState field '{fieldName}' was not found.");
        }

        return (T?)field.GetValue(state);
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

    private static Update CreateMessageUpdate(
        string text,
        long userId,
        long chatId,
        long? messageThreadId = null,
        string? businessConnectionId = null)
    {
        return new Update
        {
            UpdateId = 1,
            Message = new Message
            {
                MessageId = 10,
                Date = 0,
                MessageThreadId = messageThreadId,
                BusinessConnectionId = businessConnectionId,
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
        public ValueTask<UpdateRateLimitDecision> CheckAsync(
            UpdateContext context,
            CancellationToken cancellationToken = default)
        {
            trace.Add("rate-limit");
            return ValueTask.FromResult(UpdateRateLimitDecision.Accepted);
        }
    }

    private sealed class RejectingRateLimiter : IUpdateRateLimiter
    {
        public ValueTask<UpdateRateLimitDecision> CheckAsync(
            UpdateContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(
                UpdateRateLimitDecision.Rejected(
                    TimeSpan.FromSeconds(15),
                    "per-user-command"));
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

    private sealed class CountingStateStore(string? initialState) : IStateStore
    {
        private string? _state = initialState;

        public int GetCount { get; private set; }

        public int SetCount { get; private set; }

        public int ClearCount { get; private set; }

        public bool ThrowOnNextGet { get; set; }

        public bool ThrowOnNextSet { get; set; }

        public bool ThrowOnNextClear { get; set; }

        public ValueTask<string?> GetStateAsync(StateKey key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetCount++;

            if (ThrowOnNextGet)
            {
                ThrowOnNextGet = false;
                throw new NotSupportedException();
            }

            return ValueTask.FromResult(_state);
        }

        public ValueTask SetStateAsync(
            StateKey key,
            string state,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetCount++;

            if (ThrowOnNextSet)
            {
                ThrowOnNextSet = false;
                throw new NotSupportedException();
            }

            _state = state;
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearStateAsync(StateKey key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClearCount++;

            if (ThrowOnNextClear)
            {
                ThrowOnNextClear = false;
                throw new NotSupportedException();
            }

            _state = null;
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

    private sealed class RecordingStateStorageKeyBuilder : IStateStorageKeyBuilder
    {
        public string Build(StateKey key, StateStorageKeyPart part)
        {
            return $"{key.Namespace}:{part}";
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
            Entries.Add(new LogEntry(eventId, logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(EventId EventId, LogLevel Level, string Message, Exception? Exception);
}
