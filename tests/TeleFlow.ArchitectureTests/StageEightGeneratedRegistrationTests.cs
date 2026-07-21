using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TeleFlow.Annotations;
using TeleFlow.Framework.Application;
using TeleFlow.Framework.DependencyInjection;
using TeleFlow.Framework.States;
using TeleFlow.Framework.Updates;
using TeleFlow.Storage.Memory;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Types;

[assembly: TelegramGeneratedHandlersAttribute(typeof(TeleFlow.ArchitectureTests.StageEightGeneratedHandlersRegistrar))]

namespace TeleFlow.ArchitectureTests;

public sealed class StageEightGeneratedRegistrationTests
{
    [Theory]
    [InlineData("/start")]
    [InlineData("/start arg")]
    public async Task GeneratedRegistrar_DispatchesCommandHandlers(string text)
    {
        using var serviceProvider = CreateServiceProvider();
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate(text));

        Assert.Equal([$"command:{text}"], probe.Events);
    }

    [Fact]
    public async Task GeneratedRegistrar_SlashCommandMention_MatchesConfiguredCurrentBotOnly()
    {
        using var serviceProvider = CreateServiceProvider(
            configureBot: options => options.BotUsername = "teleflow_test_bot");
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/start@teleflow_test_bot"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/start@other_bot"));

        Assert.Equal(
            ["command:/start@teleflow_test_bot", "fallback:/start@other_bot"],
            probe.Events);
    }

    [Fact]
    public async Task GeneratedRegistrar_SlashCommandMention_DoesNotMatchWhenBotIdentityIsUnknown()
    {
        using var serviceProvider = CreateServiceProvider();
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/start@teleflow_test_bot"));

        Assert.Equal(["fallback:/start@teleflow_test_bot"], probe.Events);
    }

    [Fact]
    public async Task GeneratedRegistrar_OverlappingPrefixes_UsesLongestPrefix()
    {
        using var serviceProvider = CreateServiceProvider();
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("!!generated-overlap"));

        Assert.Equal(["generated-overlapping-prefix"], probe.Events);
    }

    [Fact]
    public async Task GeneratedRegistrar_DispatchesMessageAndCallbackHandlers()
    {
        using var serviceProvider = CreateServiceProvider();
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello"));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate("""{"id":42}"""));

        Assert.Equal(["message:hello", "callback:42"], probe.Events);
    }

    [Fact]
    public async Task GeneratedRegistrar_OptionalPrefixCommand_DoesNotMatchPrefixLessTextWithArguments()
    {
        using var serviceProvider = CreateServiceProvider();
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("Я рассказываю обычную фразу"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("я"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/я рассказываю обычную фразу"));

        Assert.Equal(
            [
                "fallback:Я рассказываю обычную фразу",
                "generated-short-command:я",
                "generated-short-command:/я рассказываю обычную фразу"
            ],
            probe.Events);
    }

    [Fact]
    public async Task GeneratedRegistrar_OptionalPrefixCommandTemplateWithoutValues_DoesNotMatchPrefixLessTextWithArguments()
    {
        using var serviceProvider = CreateServiceProvider();
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("Я рассказываю обычную фразу"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("я-темплейт"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/я-темплейт"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/я-темплейт рассказываю обычную фразу"));

        Assert.Equal(
            [
                "fallback:Я рассказываю обычную фразу",
                "generated-short-template:я-темплейт",
                "generated-short-template:/я-темплейт",
                "fallback:/я-темплейт рассказываю обычную фразу"
            ],
            probe.Events);
    }

    [Fact]
    public async Task GeneratedRegistrar_DispatchesGeneratedErrorHandlers()
    {
        using var serviceProvider = CreateServiceProvider();
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/generated-boom"));

        Assert.Equal(["generated-error:generated failed"], probe.Events);
    }

    [Fact]
    public async Task GeneratedRegistrar_PrioritizesModuleErrorHandlers()
    {
        using var serviceProvider = CreateServiceProvider();
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/generated-module-boom"));

        Assert.Equal(["generated-module-error:generated module failed"], probe.Events);
    }

    [Fact]
    public async Task AddTelegramModule_CanRegisterGeneratedErrorOnlyModule()
    {
        var services = new ServiceCollection();

        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddSingleton<GeneratedHandlerProbe>();
        services.AddTelegramHandler<DirectGeneratedOnlyModuleThrowingHandler>();
        services.AddTelegramModule<GeneratedOnlyModuleErrorHandler>();

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/generated-only-boom"));

        Assert.Equal(["generated-only-module-error:generated only failed"], probe.Events);
    }

    [Fact]
    public async Task GeneratedRegistrar_DoesNotMixReflectionScanForSameAssembly()
    {
        using var serviceProvider = CreateServiceProvider();
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/reflection-only"));

        Assert.DoesNotContain("reflection-only", probe.Events);
    }

    [Fact]
    public async Task GeneratedRegistrar_RespectsStatePriorityDispatch()
    {
        GeneratedStateUpdateSource.Payloads =
        [
            new TelegramUpdatePayload(CreateMessageUpdate("/state")),
            new TelegramUpdatePayload(CreateMessageUpdate("next"))
        ];
        var probe = new GeneratedHandlerProbe();

        var builder = TeleFlowApplication.CreateBuilder();
        builder.Services.AddTelegramBot(options => options.Token = "test-token");
        builder.Services.AddMemoryStateStorage();
        builder.Services.AddUpdateSource<GeneratedStateUpdateSource>();
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<GeneratedAllowMessageFilter>();
        builder.Services.AddSingleton<GeneratedRequireTextFilter>();
        builder.Services.AddTelegramHandlersFromAssembly(typeof(StageEightGeneratedRegistrationTests).Assembly);

        await using var application = builder.Build();
        await application.RunAsync();

        Assert.Equal(
            ["state:set", "state:next"],
            probe.Events);
    }

    [Fact]
    public async Task GeneratedRegistrar_DispatchesSceneStepMetadata()
    {
        GeneratedStateUpdateSource.Payloads =
        [
            new TelegramUpdatePayload(CreateMessageUpdate("/scene")),
            new TelegramUpdatePayload(CreateMessageUpdate("next"))
        ];
        var probe = new GeneratedHandlerProbe();

        var builder = TeleFlowApplication.CreateBuilder();
        builder.Services.AddTelegramBot(options => options.Token = "test-token");
        builder.Services.AddMemoryStateStorage();
        builder.Services.AddUpdateSource<GeneratedStateUpdateSource>();
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<GeneratedAllowMessageFilter>();
        builder.Services.AddSingleton<GeneratedRequireTextFilter>();
        builder.Services.AddTelegramHandlersFromAssembly(typeof(StageEightGeneratedRegistrationTests).Assembly);

        await using var application = builder.Build();
        await application.RunAsync();

        Assert.Equal(
            ["scene:set", "scene:next"],
            probe.Events);
    }

    [Fact]
    public async Task GeneratedRegistrar_AppliesFilterDescriptors()
    {
        using var serviceProvider = CreateServiceProvider();
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(
            serviceProvider,
            CreateMessageUpdate("generated-filter", chatType: "group", configure: message => message with { Caption = "caption" }));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("generated-filter", chatType: "private"));
        await DispatchAsync(
            serviceProvider,
            CreateMessageUpdate("generated-filter", chatType: "private", message => message with { Caption = "caption" }));

        Assert.Equal(
            ["fallback:generated-filter", "fallback:generated-filter", "generated-filter:generated-filter"],
            probe.Events);
    }

    [Fact]
    public async Task GeneratedRegistrar_AppliesParameterizedCustomFilterDescriptors()
    {
        using var serviceProvider = CreateServiceProvider();
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("GENERATED-PARAMETERIZED"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("other-parameterized"));

        Assert.Equal(["generated-parameterized:GENERATED-PARAMETERIZED", "fallback:other-parameterized"], probe.Events);
    }

    [Fact]
    public async Task GeneratedRegistrar_AppliesCallbackFilterDescriptors()
    {
        using var serviceProvider = CreateServiceProvider();
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("admin:delete"));

        Assert.Equal(["generated-callback-prefix:admin:delete"], probe.Events);
    }

    [Fact]
    public async Task GeneratedRegistrar_AppliesAutoAnswerCallbackDescriptor()
    {
        var transport = new GeneratedRecordingTelegramTransport(CreateOkResponse());
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ITelegramTransport>();
                services.AddSingleton<ITelegramTransport>(transport);
            });
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("auto:answer"));

        Assert.Equal(["generated-auto-answer:auto:answer"], probe.Events);
        var request = Assert.Single(transport.Requests);
        Assert.Equal("answerCallbackQuery", request.MethodName);
        Assert.Contains("\"text\":\"Generated\"", request.Json);
    }

    [Fact]
    public async Task GeneratedRegistrar_AppliesChatAndTopicFilterDescriptors()
    {
        using var serviceProvider = CreateServiceProvider();
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("generated-topic"));
        await DispatchAsync(
            serviceProvider,
            CreateMessageUpdate("generated-topic", configure: message => message with { MessageThreadId = 42 }));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate("chat:callback"));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate("chat:callback", includeMessage: true));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate("chat:inaccessible", inaccessibleChatId: 100));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate("type:callback", includeMessage: true));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate("type:inaccessible", inaccessibleChatId: 100));
        await DispatchAsync(
            serviceProvider,
            CreateCallbackUpdate(
                "username:callback",
                includeMessage: true,
                configureMessage: message => message with
                {
                    Chat = new Chat { Id = 200, Type = "private", Username = "Group" }
                }));
        await DispatchAsync(
            serviceProvider,
            CreateCallbackUpdate(
                "thread:callback",
                includeMessage: true,
                configureMessage: message => message with { MessageThreadId = 77 }));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate("thread:inaccessible", inaccessibleChatId: 100));

        Assert.Equal(
            [
                "fallback:generated-topic",
                "generated-topic:42",
                "generated-callback-chat:chat:callback",
                "generated-callback-type:type:callback",
                "generated-callback-username:username:callback",
                "generated-callback-thread:thread:callback"
            ],
            probe.Events);
    }

    [Fact]
    public async Task GeneratedRegistrar_AppliesTelegramRoleRequirementDescriptors()
    {
        var resolver = new GeneratedRecordingRoleResolver(TelegramMemberStatusSet.Administrator);
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ITelegramChatMemberStatusResolver>();
                services.AddSingleton<ITelegramChatMemberStatusResolver>(resolver);
            });
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("generated-role"));

        Assert.Equal(["generated-role:generated-role"], probe.Events);
        Assert.Equal([(100L, 5L)], resolver.Requests);
    }

    [Fact]
    public async Task GeneratedRegistrar_DispatchesChatMemberHandlers()
    {
        using var serviceProvider = CreateServiceProvider();
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(
            serviceProvider,
            CreateChatMemberUpdate(
                oldChatMember: LeftChatMember(),
                newChatMember: MemberChatMember()));

        Assert.Equal(["generated-chat-member:5"], probe.Events);
    }

    [Fact]
    public void GeneratedRegistrar_DoesNotReplaceCustomDispatcher()
    {
        var dispatcher = new StageEightCustomDispatcher();
        var services = new ServiceCollection();

        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddSingleton<TeleFlow.Framework.Dispatching.IUpdateDispatcher>(dispatcher);
        services.AddTelegramHandlersFromAssembly(typeof(StageEightGeneratedRegistrationTests).Assembly);

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.Same(
            dispatcher,
            serviceProvider.GetRequiredService<TeleFlow.Framework.Dispatching.IUpdateDispatcher>());
    }

    [Fact]
    public async Task AddTelegramModule_UsesGeneratedRegistrarForSingleModuleOnly()
    {
        var services = new ServiceCollection();

        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddSingleton<GeneratedHandlerProbe>();
        services.AddTelegramModule<GeneratedModuleHandler>();

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/generated-module"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/start"));

        Assert.Equal(["module:/generated-module"], probe.Events);
    }

    [Fact]
    public async Task AddTelegramModule_FallsBackToDirectRegistrationWithoutScanningAssembly()
    {
        var services = new ServiceCollection();

        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddSingleton<GeneratedHandlerProbe>();
        services.AddTelegramModule<DirectOnlyGeneratedAssemblyModule>();

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/direct-module"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/reflection-only"));

        Assert.Equal(["direct-module:/direct-module"], probe.Events);
    }

    [Fact]
    public async Task GeneratedRouteValueParameter_FailsClearlyWhenRouteDoesNotProvideValue()
    {
        using var serviceProvider = CreateServiceProvider();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DispatchAsync(serviceProvider, CreateMessageUpdate("/route-value")));

        Assert.Contains("route value 'id'", exception.Message);
        Assert.Contains("did not provide it", exception.Message);
    }

    [Fact]
    public async Task GeneratedOptionalTemplateRouteValue_DispatchesMissingAndProvidedValues()
    {
        using var serviceProvider = CreateServiceProvider();
        var probe = serviceProvider.GetRequiredService<GeneratedHandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("optional"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("optional 7"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("optional abc"));

        Assert.Equal(
            ["generated-optional:null", "generated-optional:7", "fallback:optional abc"],
            probe.Events);
    }

    private static ServiceProvider CreateServiceProvider(
        Action<IServiceCollection>? configureServices = null,
        Action<TelegramBotOptions>? configureBot = null)
    {
        var services = new ServiceCollection();
        services.AddTelegramBot(options =>
        {
            options.Token = "test-token";
            configureBot?.Invoke(options);
        });
        services.AddSingleton<GeneratedHandlerProbe>();
        services.AddSingleton<GeneratedAllowMessageFilter>();
        services.AddSingleton<GeneratedRequireTextFilter>();
        services.AddTelegramHandlersFromAssembly(typeof(StageEightGeneratedRegistrationTests).Assembly);
        configureServices?.Invoke(services);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static async Task DispatchAsync(
        ServiceProvider serviceProvider,
        Update update,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = new UpdateContext(
            scope.ServiceProvider,
            new TelegramUpdatePayload(update),
            cancellationToken);

        await scope.ServiceProvider
            .GetRequiredService<TeleFlow.Framework.Dispatching.IUpdateDispatcher>()
            .DispatchAsync(context, cancellationToken);
    }

    private static TelegramTransportResponse CreateOkResponse()
    {
        return new TelegramTransportResponse(
            200,
            """{"ok":true,"result":true}""");
    }

    private static Update CreateMessageUpdate(
        string text,
        string chatType = "private",
        Func<Message, Message>? configure = null)
    {
        var message = new Message
        {
            MessageId = 10,
            Date = 0,
            Chat = new Chat { Id = 100, Type = chatType },
            From = new User { Id = 5, IsBot = false, FirstName = "User" },
            Text = text
        };

        message = configure?.Invoke(message) ?? message;

        return new Update
        {
            UpdateId = 1,
            Message = message
        };
    }

    private static Update CreateCallbackUpdate(
        string data,
        bool includeMessage = false,
        Func<Message, Message>? configureMessage = null,
        long? inaccessibleChatId = null)
    {
        var message = new Message
        {
            MessageId = 10,
            Date = 0,
            Chat = new Chat { Id = 100, Type = "private" },
            From = new User { Id = 5, IsBot = false, FirstName = "User" },
            Text = "callback source"
        };

        message = configureMessage?.Invoke(message) ?? message;

        return new Update
        {
            UpdateId = 1,
            CallbackQuery = new CallbackQuery
            {
                Id = "cb",
                From = new User
                {
                    Id = 5,
                    IsBot = false,
                    FirstName = "User"
                },
                ChatInstance = "chat-instance",
                Data = data,
                Message = includeMessage
                    ? MaybeInaccessibleMessage.From(message)
                    : inaccessibleChatId is not null
                        ? MaybeInaccessibleMessage.From(new InaccessibleMessage
                        {
                            Chat = new Chat { Id = inaccessibleChatId.Value, Type = "private" },
                            MessageId = 10
                        })
                    : null
            }
        };
    }

    private static Update CreateChatMemberUpdate(
        ChatMember oldChatMember,
        ChatMember newChatMember)
    {
        return new Update
        {
            UpdateId = 1,
            ChatMember = new ChatMemberUpdated
            {
                Chat = new Chat { Id = 100, Type = "group" },
                From = new User { Id = 7, IsBot = false, FirstName = "Admin" },
                Date = 0,
                OldChatMember = oldChatMember,
                NewChatMember = newChatMember
            }
        };
    }

    private static ChatMember MemberChatMember()
    {
        return ChatMember.From(new ChatMemberMember
        {
            User = MemberUser()
        });
    }

    private static ChatMember LeftChatMember()
    {
        return ChatMember.From(new ChatMemberLeft
        {
            User = MemberUser()
        });
    }

    private static User MemberUser()
    {
        return new User
        {
            Id = 5,
            IsBot = false,
            FirstName = "Member"
        };
    }
}

internal sealed class StageEightGeneratedHandlersRegistrar : ITelegramGeneratedHandlerRegistrar
{
    public void Register(ITelegramGeneratedHandlerRegistry registry)
    {
        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedStartHandler),
            nameof(GeneratedStartHandler.Handle),
            TelegramGeneratedHandlerKind.Command,
            registrationOrder: 0,
            moduleName: null,
            command: "start",
            callbackPayloadType: null,
            textFilters: [],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeStart));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedMessageHandler),
            nameof(GeneratedMessageHandler.Handle),
            TelegramGeneratedHandlerKind.Message,
            registrationOrder: 1,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [new("hello", TextMatchMode.Equals, ignoreCase: true)],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeMessage));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedCallbackHandler),
            nameof(GeneratedCallbackHandler.Handle),
            TelegramGeneratedHandlerKind.Callback,
            registrationOrder: 2,
            moduleName: null,
            command: null,
            callbackPayloadType: typeof(GeneratedDeletePayload),
            textFilters: [],
            states: [],
            parameters:
            [
                new(typeof(CallbackQueryContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedDeletePayload), TelegramGeneratedHandlerParameterKind.CallbackPayload, "payload"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeCallback));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedStateStartHandler),
            nameof(GeneratedStateStartHandler.Handle),
            TelegramGeneratedHandlerKind.Command,
            registrationOrder: 3,
            moduleName: null,
            command: "state",
            callbackPayloadType: null,
            textFilters: [],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeStateStart));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedShortOptionalPrefixCommandHandler),
            nameof(GeneratedShortOptionalPrefixCommandHandler.Handle),
            TelegramGeneratedHandlerKind.Command,
            TelegramGeneratedRouteKind.CommandExact,
            routePattern: "я",
            commandPrefixes: ["/"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 50,
            moduleName: null,
            command: "я",
            callbackPayloadType: null,
            textFilters: [],
            filters: [],
            chatMemberTransitions: [],
            roleRequirements: [],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeShortOptionalPrefixCommand,
            prefixMode: CommandPrefixMode.Optional));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedShortOptionalPrefixCommandTemplateHandler),
            nameof(GeneratedShortOptionalPrefixCommandTemplateHandler.Handle),
            TelegramGeneratedHandlerKind.Command,
            TelegramGeneratedRouteKind.CommandTemplate,
            routePattern: "я-темплейт",
            commandPrefixes: ["/"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 51,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [],
            filters: [],
            chatMemberTransitions: [],
            roleRequirements: [],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeShortOptionalPrefixCommandTemplate,
            prefixMode: CommandPrefixMode.Optional));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedOverlappingPrefixCommandHandler),
            nameof(GeneratedOverlappingPrefixCommandHandler.Handle),
            TelegramGeneratedHandlerKind.Command,
            TelegramGeneratedRouteKind.CommandExact,
            routePattern: "generated-overlap",
            commandPrefixes: ["!", "!!"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 52,
            moduleName: null,
            command: "generated-overlap",
            callbackPayloadType: null,
            textFilters: [],
            filters: [],
            chatMemberTransitions: [],
            roleRequirements: [],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeGeneratedOverlappingPrefixCommand));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedFallbackMessageHandler),
            nameof(GeneratedFallbackMessageHandler.Handle),
            TelegramGeneratedHandlerKind.Message,
            registrationOrder: 5,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeFallbackMessage));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedFilteredMessageHandler),
            nameof(GeneratedFilteredMessageHandler.Handle),
            TelegramGeneratedHandlerKind.Message,
            TelegramGeneratedRouteKind.MessageAny,
            routePattern: null,
            commandPrefixes: ["/"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 4,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [],
            filters:
            [
                new(typeof(GeneratedAllowMessageFilter)),
                new(TelegramGeneratedFilterKind.ChatType, stringValues: ["private"]),
                new(TelegramGeneratedFilterKind.HasText),
                new(TelegramGeneratedFilterKind.HasCaption)
            ],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeFilteredMessage));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedParameterizedFilterMessageHandler),
            nameof(GeneratedParameterizedFilterMessageHandler.Handle),
            TelegramGeneratedHandlerKind.Message,
            TelegramGeneratedRouteKind.MessageAny,
            routePattern: null,
            commandPrefixes: ["/"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 4,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [],
            filters:
            [
             new(
                 typeof(GeneratedRequireTextFilter),
                 typeof(MessageContext),
                 new GeneratedRequireTextAttribute("generated-parameterized") { IgnoreCase = true })
         ],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeParameterizedFilterMessage));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedRoleMessageHandler),
            nameof(GeneratedRoleMessageHandler.Handle),
            TelegramGeneratedHandlerKind.Message,
            TelegramGeneratedRouteKind.TextExact,
            routePattern: "generated-role",
            commandPrefixes: ["/"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 11,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [new("generated-role", TextMatchMode.Equals, ignoreCase: true)],
            filters: [],
            chatMemberTransitions: [],
            roleRequirements:
            [
                new(TelegramMemberStatusSet.IsAdmin)
            ],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeRoleMessage));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedCallbackPrefixHandler),
            nameof(GeneratedCallbackPrefixHandler.Handle),
            TelegramGeneratedHandlerKind.Callback,
            TelegramGeneratedRouteKind.Callback,
            routePattern: null,
            commandPrefixes: ["/"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 9,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [],
            filters:
            [
                new(TelegramGeneratedFilterKind.HasCallbackData),
                new(TelegramGeneratedFilterKind.CallbackDataPrefix, stringValues: ["admin:"])
            ],
            states: [],
            parameters:
            [
                new(typeof(CallbackQueryContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeCallbackPrefix));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedCallbackChatFilterHandler),
            nameof(GeneratedCallbackChatFilterHandler.Handle),
            TelegramGeneratedHandlerKind.Callback,
            TelegramGeneratedRouteKind.Callback,
            routePattern: null,
            commandPrefixes: ["/"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 14,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [],
            filters:
            [
                new(TelegramGeneratedFilterKind.ChatId, longValues: [100]),
                new(TelegramGeneratedFilterKind.CallbackDataPrefix, stringValues: ["chat:"])
            ],
            states: [],
            parameters:
            [
                new(typeof(CallbackQueryContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeCallbackChatFilter));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedCallbackChatUsernameFilterHandler),
            nameof(GeneratedCallbackChatUsernameFilterHandler.Handle),
            TelegramGeneratedHandlerKind.Callback,
            TelegramGeneratedRouteKind.Callback,
            routePattern: null,
            commandPrefixes: ["/"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 15,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [],
            filters:
            [
                new(TelegramGeneratedFilterKind.ChatUsername, stringValues: ["Group"]),
                new(TelegramGeneratedFilterKind.CallbackDataPrefix, stringValues: ["username:"])
            ],
            states: [],
            parameters:
            [
                new(typeof(CallbackQueryContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeCallbackChatUsernameFilter));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedCallbackThreadFilterHandler),
            nameof(GeneratedCallbackThreadFilterHandler.Handle),
            TelegramGeneratedHandlerKind.Callback,
            TelegramGeneratedRouteKind.Callback,
            routePattern: null,
            commandPrefixes: ["/"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 16,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [],
            filters:
            [
                new(TelegramGeneratedFilterKind.HasMessageThread),
                new(TelegramGeneratedFilterKind.CallbackDataPrefix, stringValues: ["thread:"])
            ],
            states: [],
            parameters:
            [
                new(typeof(CallbackQueryContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeCallbackThreadFilter));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedCallbackChatTypeFilterHandler),
            nameof(GeneratedCallbackChatTypeFilterHandler.Handle),
            TelegramGeneratedHandlerKind.Callback,
            TelegramGeneratedRouteKind.Callback,
            routePattern: null,
            commandPrefixes: ["/"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 17,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [],
            filters:
            [
                new(TelegramGeneratedFilterKind.ChatType, stringValues: ["private"]),
                new(TelegramGeneratedFilterKind.CallbackDataPrefix, stringValues: ["type:"])
            ],
            states: [],
            parameters:
            [
                new(typeof(CallbackQueryContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeCallbackChatTypeFilter));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedStateMessageHandler),
            nameof(GeneratedStateMessageHandler.Handle),
            TelegramGeneratedHandlerKind.Message,
            registrationOrder: 6,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [],
            states: ["awaiting"],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeStateMessage));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedModuleHandler),
            nameof(GeneratedModuleHandler.Handle),
            TelegramGeneratedHandlerKind.Command,
            registrationOrder: 7,
            moduleName: "generated",
            command: "generated-module",
            callbackPayloadType: null,
            textFilters: [],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeModule));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedRouteValueHandler),
            nameof(GeneratedRouteValueHandler.Handle),
            TelegramGeneratedHandlerKind.Command,
            registrationOrder: 8,
            moduleName: null,
            command: "route-value",
            callbackPayloadType: null,
            textFilters: [],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(long), TelegramGeneratedHandlerParameterKind.RouteValue, "id"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeRouteValue));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedChatMemberHandler),
            nameof(GeneratedChatMemberHandler.Handle),
            TelegramGeneratedHandlerKind.ChatMember,
            TelegramGeneratedRouteKind.ChatMemberUpdated,
            routePattern: null,
            commandPrefixes: ["/"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 10,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [],
            filters:
            [
                new(TelegramGeneratedFilterKind.ChatId, longValues: [100])
            ],
            chatMemberTransitions:
            [
                new(
                    TelegramMemberStatusSet.IsNotMember,
                    TelegramMemberStatusSet.IsMember)
            ],
            states: [],
            parameters:
            [
                new(typeof(ChatMemberUpdatedContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeChatMember));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedTopicMessageHandler),
            nameof(GeneratedTopicMessageHandler.Handle),
            TelegramGeneratedHandlerKind.Message,
            TelegramGeneratedRouteKind.TextExact,
            routePattern: "generated-topic",
            commandPrefixes: ["/"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 16,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [new("generated-topic", TextMatchMode.Equals, ignoreCase: true)],
            filters:
            [
                new(TelegramGeneratedFilterKind.MessageThreadId, longValues: [42])
            ],
            chatMemberTransitions: [],
            roleRequirements: [],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeTopicMessage));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedSceneStartHandler),
            nameof(GeneratedSceneStartHandler.Handle),
            TelegramGeneratedHandlerKind.Command,
            registrationOrder: 12,
            moduleName: null,
            command: "scene",
            callbackPayloadType: null,
            textFilters: [],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeSceneStart));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedSceneMessageHandler),
            nameof(GeneratedSceneMessageHandler.Handle),
            TelegramGeneratedHandlerKind.Message,
            TelegramGeneratedRouteKind.MessageAny,
            routePattern: null,
            commandPrefixes: ["/"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 13,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [],
            filters: [],
            chatMemberTransitions: [],
            roleRequirements: [],
            states: ["generated-scene:name"],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeSceneMessage,
            sceneName: "generated-scene"));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedOptionalTemplateHandler),
            nameof(GeneratedOptionalTemplateHandler.Handle),
            TelegramGeneratedHandlerKind.Message,
            TelegramGeneratedRouteKind.TextTemplate,
            routePattern: "optional {id:long?}",
            commandPrefixes: ["/"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 14,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [],
            filters: [],
            chatMemberTransitions: [],
            roleRequirements: [],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(long?), TelegramGeneratedHandlerParameterKind.RouteValue, "id"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeOptionalTemplate,
            sceneName: null,
            routeValues:
            [
                new("id", typeof(long), isOptional: true)
            ]));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedAutoAnswerCallbackHandler),
            nameof(GeneratedAutoAnswerCallbackHandler.Handle),
            TelegramGeneratedHandlerKind.Callback,
            TelegramGeneratedRouteKind.Callback,
            routePattern: null,
            commandPrefixes: ["/"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder: 15,
            moduleName: null,
            command: null,
            callbackPayloadType: null,
            textFilters: [],
            filters:
            [
                new(TelegramGeneratedFilterKind.CallbackDataPrefix, stringValues: ["auto:"])
            ],
            chatMemberTransitions: [],
            roleRequirements: [],
            states: [],
            parameters:
            [
                new(typeof(CallbackQueryContext), TelegramGeneratedHandlerParameterKind.Context, "context"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedHandlerParameterKind.Service, "probe")
            ],
            InvokeAutoAnswerCallback,
            sceneName: null,
            routeValues: [],
            autoAnswerCallback: new TelegramGeneratedAutoAnswerCallbackDescriptor(
                enabled: true,
                text: "Generated")));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedThrowingHandler),
            nameof(GeneratedThrowingHandler.Handle),
            TelegramGeneratedHandlerKind.Command,
            registrationOrder: 100,
            moduleName: null,
            command: "generated-boom",
            callbackPayloadType: null,
            textFilters: [],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context")
            ],
            InvokeThrowing));

        registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
            typeof(GeneratedModuleThrowingHandler),
            nameof(GeneratedModuleThrowingHandler.Handle),
            TelegramGeneratedHandlerKind.Command,
            registrationOrder: 101,
            moduleName: "generated-errors",
            command: "generated-module-boom",
            callbackPayloadType: null,
            textFilters: [],
            states: [],
            parameters:
            [
                new(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context")
            ],
            InvokeModuleThrowing));

        registry.RegisterErrorHandler(new TelegramGeneratedErrorHandlerDescriptor(
            typeof(GeneratedErrorOnlyHandler),
            nameof(GeneratedErrorOnlyHandler.Handle),
            exceptionType: typeof(GeneratedFailureException),
            telegramContextType: typeof(MessageContext),
            registrationOrder: 0,
            moduleName: null,
            parameters:
            [
                new(typeof(TelegramErrorContext), TelegramGeneratedErrorHandlerParameterKind.ErrorContext, "error"),
                new(typeof(MessageContext), TelegramGeneratedErrorHandlerParameterKind.TelegramContext, "context"),
                new(typeof(GeneratedFailureException), TelegramGeneratedErrorHandlerParameterKind.Exception, "exception"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedErrorHandlerParameterKind.Service, "probe")
            ],
            InvokeGeneratedError));

        registry.RegisterErrorHandler(new TelegramGeneratedErrorHandlerDescriptor(
            typeof(GeneratedModuleErrorHandler),
            nameof(GeneratedModuleErrorHandler.Handle),
            exceptionType: null,
            telegramContextType: typeof(MessageContext),
            registrationOrder: 1,
            moduleName: "generated-errors",
            parameters:
            [
                new(typeof(Exception), TelegramGeneratedErrorHandlerParameterKind.Exception, "exception"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedErrorHandlerParameterKind.Service, "probe")
            ],
            InvokeGeneratedModuleError));

        registry.RegisterErrorHandler(new TelegramGeneratedErrorHandlerDescriptor(
            typeof(GeneratedGlobalModuleErrorHandler),
            nameof(GeneratedGlobalModuleErrorHandler.Handle),
            exceptionType: typeof(GeneratedModuleFailureException),
            telegramContextType: typeof(MessageContext),
            registrationOrder: 2,
            moduleName: null,
            parameters:
            [
                new(typeof(GeneratedModuleFailureException), TelegramGeneratedErrorHandlerParameterKind.Exception, "exception"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedErrorHandlerParameterKind.Service, "probe")
            ],
            InvokeGeneratedGlobalModuleError));

        registry.RegisterErrorHandler(new TelegramGeneratedErrorHandlerDescriptor(
            typeof(GeneratedOnlyModuleErrorHandler),
            nameof(GeneratedOnlyModuleErrorHandler.Handle),
            exceptionType: typeof(GeneratedOnlyFailureException),
            telegramContextType: typeof(MessageContext),
            registrationOrder: 3,
            moduleName: "generated-only",
            parameters:
            [
                new(typeof(GeneratedOnlyFailureException), TelegramGeneratedErrorHandlerParameterKind.Exception, "exception"),
                new(typeof(GeneratedHandlerProbe), TelegramGeneratedErrorHandlerParameterKind.Service, "probe")
            ],
            InvokeGeneratedOnlyModuleError));
    }

    private static async ValueTask InvokeStart(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedStartHandler)services.GetRequiredService(typeof(GeneratedStartHandler));
        await handler.Handle((MessageContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeMessage(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedMessageHandler)services.GetRequiredService(typeof(GeneratedMessageHandler));
        await handler.Handle((MessageContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeCallback(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedCallbackHandler)services.GetRequiredService(typeof(GeneratedCallbackHandler));
        await handler.Handle(
            (CallbackQueryContext)arguments[0]!,
            (GeneratedDeletePayload)arguments[1]!,
            (GeneratedHandlerProbe)arguments[2]!);
    }

    private static async ValueTask InvokeStateStart(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedStateStartHandler)services.GetRequiredService(typeof(GeneratedStateStartHandler));
        await handler.Handle((MessageContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeShortOptionalPrefixCommand(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedShortOptionalPrefixCommandHandler)services.GetRequiredService(
            typeof(GeneratedShortOptionalPrefixCommandHandler));
        await handler.Handle((MessageContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeShortOptionalPrefixCommandTemplate(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedShortOptionalPrefixCommandTemplateHandler)services.GetRequiredService(
            typeof(GeneratedShortOptionalPrefixCommandTemplateHandler));
        await handler.Handle((MessageContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeGeneratedOverlappingPrefixCommand(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedOverlappingPrefixCommandHandler)services.GetRequiredService(
            typeof(GeneratedOverlappingPrefixCommandHandler));
        await handler.Handle((MessageContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeFallbackMessage(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedFallbackMessageHandler)services.GetRequiredService(typeof(GeneratedFallbackMessageHandler));
        await handler.Handle((MessageContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeFilteredMessage(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedFilteredMessageHandler)services.GetRequiredService(typeof(GeneratedFilteredMessageHandler));
        await handler.Handle((MessageContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeParameterizedFilterMessage(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedParameterizedFilterMessageHandler)services.GetRequiredService(typeof(GeneratedParameterizedFilterMessageHandler));
        await handler.Handle((MessageContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeRoleMessage(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedRoleMessageHandler)services.GetRequiredService(typeof(GeneratedRoleMessageHandler));
        await handler.Handle((MessageContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeCallbackPrefix(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedCallbackPrefixHandler)services.GetRequiredService(typeof(GeneratedCallbackPrefixHandler));
        await handler.Handle((CallbackQueryContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeCallbackChatFilter(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedCallbackChatFilterHandler)services.GetRequiredService(typeof(GeneratedCallbackChatFilterHandler));
        await handler.Handle((CallbackQueryContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeCallbackChatUsernameFilter(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedCallbackChatUsernameFilterHandler)services.GetRequiredService(typeof(GeneratedCallbackChatUsernameFilterHandler));
        await handler.Handle((CallbackQueryContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeCallbackThreadFilter(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedCallbackThreadFilterHandler)services.GetRequiredService(typeof(GeneratedCallbackThreadFilterHandler));
        await handler.Handle((CallbackQueryContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeCallbackChatTypeFilter(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedCallbackChatTypeFilterHandler)services.GetRequiredService(typeof(GeneratedCallbackChatTypeFilterHandler));
        await handler.Handle((CallbackQueryContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeStateMessage(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedStateMessageHandler)services.GetRequiredService(typeof(GeneratedStateMessageHandler));
        await handler.Handle((MessageContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeModule(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedModuleHandler)services.GetRequiredService(typeof(GeneratedModuleHandler));
        await handler.Handle((MessageContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeRouteValue(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedRouteValueHandler)services.GetRequiredService(typeof(GeneratedRouteValueHandler));
        await handler.Handle(
            (MessageContext)arguments[0]!,
            (long)arguments[1]!,
            (GeneratedHandlerProbe)arguments[2]!);
    }

    private static async ValueTask InvokeChatMember(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedChatMemberHandler)services.GetRequiredService(typeof(GeneratedChatMemberHandler));
        await handler.Handle((ChatMemberUpdatedContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeTopicMessage(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedTopicMessageHandler)services.GetRequiredService(typeof(GeneratedTopicMessageHandler));
        await handler.Handle((MessageContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeSceneStart(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedSceneStartHandler)services.GetRequiredService(typeof(GeneratedSceneStartHandler));
        await handler.Handle((MessageContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeSceneMessage(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedSceneMessageHandler)services.GetRequiredService(typeof(GeneratedSceneMessageHandler));
        await handler.Handle((MessageContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeOptionalTemplate(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedOptionalTemplateHandler)services.GetRequiredService(typeof(GeneratedOptionalTemplateHandler));
        await handler.Handle(
            (MessageContext)arguments[0]!,
            (long?)arguments[1],
            (GeneratedHandlerProbe)arguments[2]!);
    }

    private static async ValueTask InvokeAutoAnswerCallback(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedAutoAnswerCallbackHandler)services.GetRequiredService(typeof(GeneratedAutoAnswerCallbackHandler));
        await handler.Handle((CallbackQueryContext)arguments[0]!, (GeneratedHandlerProbe)arguments[1]!);
    }

    private static async ValueTask InvokeThrowing(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedThrowingHandler)services.GetRequiredService(typeof(GeneratedThrowingHandler));
        await handler.Handle((MessageContext)arguments[0]!);
    }

    private static async ValueTask InvokeModuleThrowing(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedModuleThrowingHandler)services.GetRequiredService(typeof(GeneratedModuleThrowingHandler));
        await handler.Handle((MessageContext)arguments[0]!);
    }

    private static async ValueTask<TelegramErrorHandlingResult> InvokeGeneratedError(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedErrorOnlyHandler)services.GetRequiredService(typeof(GeneratedErrorOnlyHandler));
        return await handler.Handle(
            (TelegramErrorContext)arguments[0]!,
            (MessageContext)arguments[1]!,
            (GeneratedFailureException)arguments[2]!,
            (GeneratedHandlerProbe)arguments[3]!);
    }

    private static ValueTask<TelegramErrorHandlingResult> InvokeGeneratedModuleError(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedModuleErrorHandler)services.GetRequiredService(typeof(GeneratedModuleErrorHandler));
        return ValueTask.FromResult(handler.Handle(
            (Exception)arguments[0]!,
            (GeneratedHandlerProbe)arguments[1]!));
    }

    private static ValueTask<TelegramErrorHandlingResult> InvokeGeneratedGlobalModuleError(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedGlobalModuleErrorHandler)services.GetRequiredService(typeof(GeneratedGlobalModuleErrorHandler));
        return ValueTask.FromResult(handler.Handle(
            (GeneratedModuleFailureException)arguments[0]!,
            (GeneratedHandlerProbe)arguments[1]!));
    }

    private static ValueTask<TelegramErrorHandlingResult> InvokeGeneratedOnlyModuleError(
        IServiceProvider services,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        var handler = (GeneratedOnlyModuleErrorHandler)services.GetRequiredService(typeof(GeneratedOnlyModuleErrorHandler));
        return ValueTask.FromResult(handler.Handle(
            (GeneratedOnlyFailureException)arguments[0]!,
            (GeneratedHandlerProbe)arguments[1]!));
    }
}

public sealed class GeneratedStartHandler
{
    public Task Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"command:{context.TelegramMessage.Text}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedMessageHandler
{
    public ValueTask Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"message:{context.TelegramMessage.Text}");
        return ValueTask.CompletedTask;
    }
}

public sealed class GeneratedCallbackHandler
{
    public Task Handle(
        CallbackQueryContext context,
        GeneratedDeletePayload payload,
        GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"callback:{payload.Id}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedAutoAnswerCallbackHandler
{
    public Task Handle(CallbackQueryContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-auto-answer:{context.TelegramCallbackQuery.Data}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedFailureException : InvalidOperationException
{
    public GeneratedFailureException()
        : base("generated failed")
    {
    }
}

public sealed class GeneratedThrowingHandler
{
    public Task Handle(MessageContext context)
    {
        return Task.FromException(new GeneratedFailureException());
    }
}

public sealed class GeneratedErrorOnlyHandler
{
    public Task<TelegramErrorHandlingResult> Handle(
        TelegramErrorContext error,
        MessageContext context,
        GeneratedFailureException exception,
        GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-error:{exception.Message}");
        return Task.FromResult(TelegramErrorHandlingResult.Handled);
    }
}

public sealed class GeneratedModuleFailureException : InvalidOperationException
{
    public GeneratedModuleFailureException()
        : base("generated module failed")
    {
    }
}

public sealed class GeneratedModuleThrowingHandler
{
    public Task Handle(MessageContext context)
    {
        return Task.FromException(new GeneratedModuleFailureException());
    }
}

public sealed class GeneratedModuleErrorHandler
{
    public TelegramErrorHandlingResult Handle(Exception exception, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-module-error:{exception.Message}");
        return TelegramErrorHandlingResult.Handled;
    }
}

public sealed class GeneratedGlobalModuleErrorHandler
{
    public TelegramErrorHandlingResult Handle(GeneratedModuleFailureException exception, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-global-module-error:{exception.Message}");
        return TelegramErrorHandlingResult.Handled;
    }
}

[TelegramModule("generated-only")]
public sealed class DirectGeneratedOnlyModuleThrowingHandler
{
    [Command("generated-only-boom")]
    public Task Handle(MessageContext context)
    {
        return Task.FromException(new GeneratedOnlyFailureException());
    }
}

public sealed class GeneratedOnlyFailureException : InvalidOperationException
{
    public GeneratedOnlyFailureException()
        : base("generated only failed")
    {
    }
}

[TelegramModule("generated-only")]
public sealed class GeneratedOnlyModuleErrorHandler
{
    public TelegramErrorHandlingResult Handle(GeneratedOnlyFailureException exception, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-only-module-error:{exception.Message}");
        return TelegramErrorHandlingResult.Handled;
    }
}

public sealed class GeneratedStateStartHandler
{
    public async Task Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        await context.State.SetAsync("awaiting");
        probe.Events.Add("state:set");
    }
}

public sealed class GeneratedShortOptionalPrefixCommandHandler
{
    public Task Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-short-command:{context.TelegramMessage.Text}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedShortOptionalPrefixCommandTemplateHandler
{
    public Task Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-short-template:{context.TelegramMessage.Text}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedOverlappingPrefixCommandHandler
{
    public Task Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add("generated-overlapping-prefix");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedFallbackMessageHandler
{
    public Task Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"fallback:{context.TelegramMessage.Text}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedFilteredMessageHandler
{
    public Task Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-filter:{context.TelegramMessage.Text}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedParameterizedFilterMessageHandler
{
    public Task Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-parameterized:{context.TelegramMessage.Text}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedRoleMessageHandler
{
    public Task Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-role:{context.TelegramMessage.Text}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedCallbackPrefixHandler
{
    public Task Handle(CallbackQueryContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-callback-prefix:{context.TelegramCallbackQuery.Data}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedCallbackChatFilterHandler
{
    public Task Handle(CallbackQueryContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-callback-chat:{context.TelegramCallbackQuery.Data}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedCallbackChatUsernameFilterHandler
{
    public Task Handle(CallbackQueryContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-callback-username:{context.TelegramCallbackQuery.Data}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedCallbackThreadFilterHandler
{
    public Task Handle(CallbackQueryContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-callback-thread:{context.TelegramCallbackQuery.Data}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedCallbackChatTypeFilterHandler
{
    public Task Handle(CallbackQueryContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-callback-type:{context.TelegramCallbackQuery.Data}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedStateMessageHandler
{
    public async Task Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"state:{context.TelegramMessage.Text}");
        await context.State.ClearAsync();
    }
}

[TelegramModule("generated")]
public sealed class GeneratedModuleHandler
{
    [Command("generated-module")]
    public Task Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"module:{context.TelegramMessage.Text}");
        return Task.CompletedTask;
    }
}

[TelegramModule("direct-only")]
public sealed class DirectOnlyGeneratedAssemblyModule
{
    [Command("direct-module")]
    public Task Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"direct-module:{context.TelegramMessage.Text}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedRouteValueHandler
{
    public Task Handle(MessageContext context, long id, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"route-value:{id}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedChatMemberHandler
{
    public Task Handle(ChatMemberUpdatedContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-chat-member:{context.Member.Id}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedTopicMessageHandler
{
    public Task Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-topic:{context.TelegramMessage.MessageThreadId}");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedSceneStartHandler
{
    public async Task Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        await context.State.SetAsync(State.Create("generated-scene:name"));
        probe.Events.Add("scene:set");
    }
}

public sealed class GeneratedSceneMessageHandler
{
    public async Task Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"scene:{context.TelegramMessage.Text}");
        await context.State.ResetAsync();
    }
}

public sealed class GeneratedOptionalTemplateHandler
{
    public Task Handle(MessageContext context, long? id, GeneratedHandlerProbe probe)
    {
        probe.Events.Add($"generated-optional:{id?.ToString() ?? "null"}");
        return Task.CompletedTask;
    }
}

public sealed class ReflectionOnlyHandler
{
    [Command("reflection-only")]
    public Task Handle(MessageContext context, GeneratedHandlerProbe probe)
    {
        probe.Events.Add("reflection-only");
        return Task.CompletedTask;
    }
}

public sealed class GeneratedHandlerProbe
{
    public List<string> Events { get; } = [];
}

public sealed class GeneratedAllowMessageFilter : ITelegramFilter<MessageContext>
{
    public ValueTask<bool> MatchesAsync(
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(true);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class GeneratedRequireTextAttribute : TelegramFilterAttribute<GeneratedRequireTextFilter>
{
    public GeneratedRequireTextAttribute(string text)
    {
        Text = text;
    }

    public string Text { get; }

    public bool IgnoreCase { get; set; }
}

public sealed class GeneratedRequireTextFilter : ITelegramFilter<MessageContext, GeneratedRequireTextAttribute>
{
    public ValueTask<bool> MatchesAsync(
        MessageContext context,
        GeneratedRequireTextAttribute attribute,
        CancellationToken cancellationToken = default)
    {
        var comparison = attribute.IgnoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var matches = string.Equals(context.TelegramMessage.Text, attribute.Text, comparison);
        return ValueTask.FromResult(matches);
    }
}

public sealed record GeneratedDeletePayload(long Id);

internal sealed class GeneratedStateUpdateSource : IUpdateSource
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

internal sealed class StageEightCustomDispatcher : TeleFlow.Framework.Dispatching.IUpdateDispatcher
{
    public Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

internal sealed class GeneratedRecordingTelegramTransport(params TelegramTransportResponse[] responses) : ITelegramTransport
{
    private readonly Queue<TelegramTransportResponse> _responses = new(responses);

    public List<GeneratedRecordedTelegramTransportRequest> Requests { get; } = [];

    public Task<TelegramTransportResponse> SendAsync(
        TelegramTransportRequest request,
        CancellationToken cancellationToken = default)
    {
        var json = request.Content is TelegramJsonTransportContent jsonContent
            ? jsonContent.Json
            : string.Empty;
        Requests.Add(new GeneratedRecordedTelegramTransportRequest(request.MethodName, json));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No queued Telegram transport responses remain.");
        }

        return Task.FromResult(_responses.Dequeue());
    }
}

internal sealed record GeneratedRecordedTelegramTransportRequest(string MethodName, string Json);

internal sealed class GeneratedRecordingRoleResolver(TelegramMemberStatusSet status) : ITelegramChatMemberStatusResolver
{
    public List<(long ChatId, long UserId)> Requests { get; } = [];

    public ValueTask<TelegramMemberStatusSet?> ResolveAsync(
        TelegramUpdateContext context,
        long chatId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        Requests.Add((chatId, userId));
        return ValueTask.FromResult<TelegramMemberStatusSet?>(status);
    }
}
