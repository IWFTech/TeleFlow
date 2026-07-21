using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TeleFlow.Annotations;
using TeleFlow.Framework.Application;
using TeleFlow.Framework.Callbacks;
using TeleFlow.Framework.Middleware;
using TeleFlow.Framework.States;
using TeleFlow.Framework.Updates;
using TeleFlow.Storage.Memory;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Schema.Methods;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.ArchitectureTests;

public sealed class TelegramHandlerDispatcherTests
{
    [Theory]
    [InlineData("/start")]
    [InlineData("/start arg")]
    public async Task CommandHandler_DispatchesForSupportedCommandShapes(string text)
    {
        using var cancellation = new CancellationTokenSource();
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<StartCommandHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate(text), cancellation.Token);

        Assert.Equal([$"command:{text}:True"], probe.Events);
    }

    [Fact]
    public async Task ClassBasedCommandHandler_Dispatches()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<ClassBasedStartHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/classstart"));

        Assert.Equal(["class-start:/classstart"], probe.Events);
    }

    [Fact]
    public async Task ClassBasedTemplateHandler_BindsRouteValuesAndServices()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<ClassBasedOrderHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("class order 42"));

        Assert.Equal(["class-order:42"], probe.Events);
    }

    [Fact]
    public async Task ClassBasedTypedCallbackHandler_BindsPayload()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<ClassBasedTypedCallbackHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("""{"id":77}"""));

        Assert.Equal(["class-callback:77"], probe.Events);
    }

    [Fact]
    public async Task ClassBasedChatMemberHandler_Dispatches()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<ClassBasedJoinChatMemberHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(
            serviceProvider,
            CreateChatMemberUpdate(
                isOwnUpdate: false,
                oldChatMember: LeftChatMember(),
                newChatMember: MemberChatMember()));

        Assert.Equal(["class-chat-member:5"], probe.Events);
    }

    [Fact]
    public void ClassBasedHandlerWithoutHandleAsync_FailsRegistrationClearly()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<ClassBasedMissingHandleHandler>());

        Assert.Contains("HandleAsync", exception.Message);
    }

    [Fact]
    public void ClassLevelRouteWithoutClassBasedHandler_FailsRegistrationClearly()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<ClassLevelRouteWithoutBaseHandler>());

        Assert.Contains("class-level route metadata", exception.Message);
    }

    [Fact]
    public async Task Dispatcher_LogsMatchedHandlerAndTimings()
    {
        var loggerFactory = new RecordingLoggerFactory();
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.AddTelegramHandler<StartCommandHandler>();
            });

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/start"));

        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Debug &&
                     entry.EventId.Id == 2 &&
                     entry.Category.EndsWith("TelegramHandlerDispatcher", StringComparison.Ordinal) &&
                     entry.Message.Contains("Telegram handler matched", StringComparison.Ordinal) &&
                     entry.Message.Contains("handler=StartCommandHandler.Handle", StringComparison.Ordinal) &&
                     entry.Message.Contains("route=CommandExact('start')", StringComparison.Ordinal) &&
                     entry.Message.Contains("match_ms=", StringComparison.Ordinal));
        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Debug &&
                     entry.EventId.Id == 4 &&
                     entry.Message.Contains("Telegram route execution completed", StringComparison.Ordinal) &&
                     entry.Message.Contains("handler_ms=", StringComparison.Ordinal) &&
                     entry.Message.Contains("telegram_request_count=0", StringComparison.Ordinal) &&
                     entry.Message.Contains("telegram_request_ms=0", StringComparison.Ordinal) &&
                     entry.Message.Contains("handler_logic_ms=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dispatcher_LogsHandlerTimingForSingleTelegramRequest()
    {
        var loggerFactory = new RecordingLoggerFactory();
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.RemoveAll<ITelegramTransport>();
                services.AddSingleton<ITelegramTransport>(
                    new SequencedTelegramTransport(CreateGetMeResponse()));
                services.AddTelegramHandler<OneTelegramRequestMessageHandler>();
            });

        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello"));

        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Debug &&
                     entry.EventId.Id == 4 &&
                     entry.Message.Contains("Telegram route execution completed", StringComparison.Ordinal) &&
                     entry.Message.Contains("handler=OneTelegramRequestMessageHandler.Handle", StringComparison.Ordinal) &&
                     entry.Message.Contains("telegram_request_count=1", StringComparison.Ordinal) &&
                     entry.Message.Contains("telegram_request_ms=", StringComparison.Ordinal) &&
                     entry.Message.Contains("handler_logic_ms=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dispatcher_LogsMatchedHandlerAtInformationWithoutTiming()
    {
        var loggerFactory = new RecordingLoggerFactory(LogLevel.Information);
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.AddTelegramHandler<StartCommandHandler>();
            });

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/start"));

        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Information &&
                     entry.EventId.Id == 2 &&
                     entry.Message.Contains("Telegram handler matched", StringComparison.Ordinal) &&
                     entry.Message.Contains("handler=StartCommandHandler.Handle", StringComparison.Ordinal) &&
                     entry.Message.Contains("route=CommandExact('start')", StringComparison.Ordinal) &&
                     !entry.Message.Contains("match_ms=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dispatcher_RecordsTelegramRequestTimingForTransportAttemptOnly()
    {
        var loggerFactory = new RecordingLoggerFactory();
        var timeProvider = new SequencedTimestampTimeProvider(0, 0, 0, 10, 20, 100);
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(timeProvider);
                services.RemoveAll<ITelegramTransport>();
                services.AddSingleton<ITelegramTransport>(
                    new SequencedTelegramTransport(CreateGetMeResponse()));
                services.AddTelegramHandler<OneTelegramRequestMessageHandler>();
            });

        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello"));

        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Debug &&
                     entry.EventId.Id == 4 &&
                     entry.Message.Contains("Telegram route execution completed", StringComparison.Ordinal) &&
                     entry.Message.Contains("handler=OneTelegramRequestMessageHandler.Handle", StringComparison.Ordinal) &&
                     entry.Message.Contains("telegram_request_count=1", StringComparison.Ordinal) &&
                     entry.Message.Contains("telegram_request_ms=10", StringComparison.Ordinal) &&
                     entry.Message.Contains("handler_logic_ms=90", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dispatcher_LogsHandlerTimingForMultipleTelegramRequests()
    {
        var loggerFactory = new RecordingLoggerFactory();
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.RemoveAll<ITelegramTransport>();
                services.AddSingleton<ITelegramTransport>(
                    new SequencedTelegramTransport(CreateGetMeResponse(), CreateGetMeResponse()));
                services.AddTelegramHandler<TwoTelegramRequestsCallbackHandler>();
            });

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("raw"));

        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Debug &&
                     entry.EventId.Id == 4 &&
                     entry.Message.Contains("Telegram route execution completed", StringComparison.Ordinal) &&
                     entry.Message.Contains("handler=TwoTelegramRequestsCallbackHandler.Handle", StringComparison.Ordinal) &&
                     entry.Message.Contains("telegram_request_count=2", StringComparison.Ordinal) &&
                     entry.Message.Contains("telegram_request_ms=", StringComparison.Ordinal) &&
                     entry.Message.Contains("handler_logic_ms=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dispatcher_DoesNotLogHandlerTimingWhenDebugDisabled()
    {
        var loggerFactory = new RecordingLoggerFactory(LogLevel.Information);
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.RemoveAll<ITelegramTransport>();
                services.AddSingleton<ITelegramTransport>(
                    new SequencedTelegramTransport(CreateGetMeResponse()));
                services.AddTelegramHandler<OneTelegramRequestMessageHandler>();
            });

        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello"));

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        Assert.Equal(["one-request:42"], probe.Events);
        Assert.DoesNotContain(
            loggerFactory.Entries,
            entry => entry.Category.EndsWith("TelegramHandlerDispatcher", StringComparison.Ordinal) &&
                     entry.Message.Contains("Telegram route execution completed", StringComparison.Ordinal));
        Assert.DoesNotContain(
            loggerFactory.Entries,
            entry => entry.Category.EndsWith("TelegramHandlerDispatcher", StringComparison.Ordinal) &&
                     entry.Message.Contains("telegram_request_count=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dispatcher_LogsNoMatchedHandler()
    {
        var loggerFactory = new RecordingLoggerFactory();
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.AddTelegramHandler<ExactTextMessageHandler>();
            });

        await DispatchAsync(serviceProvider, CreateMessageUpdate("not matched"));

        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Debug &&
                     entry.EventId.Id == 1 &&
                     entry.Message.Contains("No Telegram handler matched", StringComparison.Ordinal) &&
                     entry.Message.Contains("type=message", StringComparison.Ordinal) &&
                     entry.Message.Contains("match_ms=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dispatcher_LogsNoMatchedHandlerAtInformationWithoutTiming()
    {
        var loggerFactory = new RecordingLoggerFactory(LogLevel.Information);
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.AddTelegramHandler<ExactTextMessageHandler>();
            });

        await DispatchAsync(serviceProvider, CreateMessageUpdate("not matched"));

        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Information &&
                     entry.EventId.Id == 1 &&
                     entry.Message.Contains("No Telegram handler matched", StringComparison.Ordinal) &&
                     entry.Message.Contains("type=message", StringComparison.Ordinal) &&
                     !entry.Message.Contains("match_ms=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dispatcher_LogsFailedHandlerAndRethrowsOriginalException()
    {
        var loggerFactory = new RecordingLoggerFactory();
        var expected = new InvalidOperationException("handler failed");
        ThrowingMessageHandler.Exception = expected;
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.AddTelegramHandler<ThrowingMessageHandler>();
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DispatchAsync(serviceProvider, CreateMessageUpdate("boom")));

        Assert.Same(expected, exception);
        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Error &&
                     entry.EventId.Id == 3 &&
                     ReferenceEquals(entry.Exception, expected) &&
                     entry.Message.Contains("Telegram route execution failed", StringComparison.Ordinal) &&
                     entry.Message.Contains("handler=ThrowingMessageHandler.Handle", StringComparison.Ordinal) &&
                     entry.Message.Contains("handler_ms=", StringComparison.Ordinal) &&
                     entry.Message.Contains("telegram_request_count=0", StringComparison.Ordinal) &&
                     entry.Message.Contains("telegram_request_ms=0", StringComparison.Ordinal) &&
                     entry.Message.Contains("handler_logic_ms=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dispatcher_LogsFailedHandlerWithoutTimingWhenDebugDisabled()
    {
        var loggerFactory = new RecordingLoggerFactory(LogLevel.Information);
        var expected = new InvalidOperationException("handler failed");
        ThrowingMessageHandler.Exception = expected;
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.AddTelegramHandler<ThrowingMessageHandler>();
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DispatchAsync(serviceProvider, CreateMessageUpdate("boom")));

        Assert.Same(expected, exception);
        var entry = Assert.Single(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Error &&
                     entry.EventId.Id == 3 &&
                     ReferenceEquals(entry.Exception, expected) &&
                     entry.Message.Contains("Telegram route execution failed", StringComparison.Ordinal));

        Assert.Contains("handler=ThrowingMessageHandler.Handle", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("handler_ms=", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("telegram_request_count=", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("telegram_request_ms=", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("handler_logic_ms=", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ErrorHandler_HandlesSelectedHandlerException()
    {
        var expected = new InvalidOperationException("handler failed");
        ThrowingMessageHandler.Exception = expected;
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<ThrowingMessageHandler>();
                services.AddTelegramHandler<HandledErrorHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("boom"));

        Assert.Equal(["handled:handler failed"], probe.Events);
    }

    [Fact]
    public async Task ErrorHandler_LogsCompletionWhenDebugEnabled()
    {
        var loggerFactory = new RecordingLoggerFactory();
        var expected = new InvalidOperationException("handler failed");
        ThrowingMessageHandler.Exception = expected;
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.AddTelegramHandler<ThrowingMessageHandler>();
                services.AddTelegramHandler<HandledErrorHandler>();
            });

        await DispatchAsync(serviceProvider, CreateMessageUpdate("boom"));

        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Debug &&
                     entry.EventId.Id == 5 &&
                     entry.Message.Contains("Telegram error handler completed", StringComparison.Ordinal) &&
                     entry.Message.Contains("handler=ThrowingMessageHandler.Handle", StringComparison.Ordinal) &&
                     entry.Message.Contains("error_handler=HandledErrorHandler.Handle", StringComparison.Ordinal) &&
                     entry.Message.Contains("handled=True", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ErrorHandler_DoesNotLogCompletionWhenDebugDisabled()
    {
        var loggerFactory = new RecordingLoggerFactory(LogLevel.Information);
        var expected = new InvalidOperationException("handler failed");
        ThrowingMessageHandler.Exception = expected;
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.AddTelegramHandler<ThrowingMessageHandler>();
                services.AddTelegramHandler<HandledErrorHandler>();
            });

        await DispatchAsync(serviceProvider, CreateMessageUpdate("boom"));

        Assert.DoesNotContain(
            loggerFactory.Entries,
            entry => entry.Message.Contains("Telegram error handler completed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ErrorHandler_UnhandledResultRethrowsOriginalException()
    {
        var expected = new InvalidOperationException("handler failed");
        ThrowingMessageHandler.Exception = expected;
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<ThrowingMessageHandler>();
                services.AddTelegramHandler<UnhandledErrorHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DispatchAsync(serviceProvider, CreateMessageUpdate("boom")));

        Assert.Same(expected, exception);
        Assert.Equal(["unhandled:handler failed"], probe.Events);
    }

    [Fact]
    public async Task ErrorHandler_ModuleCandidateRunsBeforeGlobalCandidate()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<ModuleThrowingHandler>();
                services.AddTelegramHandler<ModuleScopedErrorHandler>();
                services.AddTelegramHandler<GlobalModuleErrorHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/module-boom"));

        Assert.Equal(["module-error:module failed"], probe.Events);
    }

    [Fact]
    public async Task ErrorHandler_MoreSpecificExceptionRunsBeforeBroadException()
    {
        ThrowingMessageHandler.Exception = new SpecificErrorHandlerFailureException();
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<ThrowingMessageHandler>();
                services.AddTelegramHandler<BroadInvalidOperationErrorHandler>();
                services.AddTelegramHandler<SpecificFailureErrorHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("boom"));

        Assert.Equal(["specific-error:specific failure"], probe.Events);
    }

    [Fact]
    public async Task ErrorHandler_RegistrationOrderBreaksSpecificityTies()
    {
        ThrowingMessageHandler.Exception = new InvalidOperationException("handler failed");
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<ThrowingMessageHandler>();
                services.AddTelegramHandler<FirstTieErrorHandler>();
                services.AddTelegramHandler<SecondTieErrorHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("boom"));

        Assert.Equal(["first-tie:handler failed", "second-tie:handler failed"], probe.Events);
    }

    [Fact]
    public async Task ErrorHandler_CatchAllRunsAfterTypedHandlers()
    {
        ThrowingMessageHandler.Exception = new InvalidOperationException("handler failed");
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<ThrowingMessageHandler>();
                services.AddTelegramHandler<TypedUnhandledErrorHandler>();
                services.AddTelegramHandler<CatchAllHandledErrorHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("boom"));

        Assert.Equal(["typed-unhandled:handler failed", "catch-all:handler failed"], probe.Events);
    }

    [Fact]
    public async Task ErrorHandler_BindsContextExceptionRouteValuesServicesAndCancellationToken()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<RouteValueThrowingHandler>();
                services.AddTelegramHandler<RouteValueErrorHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/orders 77"));

        Assert.Equal(
            ["bound:77:RouteValueFailureException:Handle:RouteValueThrowingHandler:MessageContext:False"],
            probe.Events);
    }

    [Fact]
    public async Task ErrorHandler_SkipsIncompatibleRouteValueHandler()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<RouteValueThrowingHandler>();
                services.AddTelegramHandler<IncompatibleRouteValueErrorHandler>();
                services.AddTelegramHandler<RouteValueErrorHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/orders 77"));

        Assert.Equal(
            ["bound:77:RouteValueFailureException:Handle:RouteValueThrowingHandler:MessageContext:False"],
            probe.Events);
    }

    [Fact]
    public void ErrorHandler_InvalidReturnTypeFailsRegistration()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<InvalidErrorReturnHandler>());

        Assert.Contains(nameof(TelegramErrorHandlingResult), exception.Message);
    }

    [Fact]
    public async Task ErrorHandler_InvalidResultValueFailsClearly()
    {
        ThrowingMessageHandler.Exception = new InvalidOperationException("handler failed");
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<ThrowingMessageHandler>();
                services.AddTelegramHandler<InvalidErrorResultHandler>();
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DispatchAsync(serviceProvider, CreateMessageUpdate("boom")));

        Assert.Contains("unsupported error handling result", exception.Message);
    }

    [Fact]
    public async Task ErrorHandler_CancellationBypassesErrorHandlers()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<CancellableThrowingHandler>();
                services.AddTelegramHandler<HandledErrorHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => DispatchAsync(serviceProvider, CreateMessageUpdate("cancel"), cancellation.Token));

        Assert.Empty(probe.Events);
    }

    [Fact]
    public async Task MessageHandlers_DispatchInRegistrationOrder_AndRespectTextFilters()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<ExactTextMessageHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("other"));

        Assert.Equal(["text:hello", "message:other"], probe.Events);
    }

    [Fact]
    public async Task TextAttributeWithoutMessage_IsExactLiteralRoute()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<LiteralTextRouteHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("order {id}"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("order 42"));

        Assert.Equal(["literal-text:order {id}"], probe.Events);
    }

    [Fact]
    public async Task CommandAndTextAttributes_CanInvokeSameHandler()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<CommandAndTextRouteHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/help"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("help"));

        Assert.Equal(["command-or-text:/help", "command-or-text:help"], probe.Events);
    }

    [Theory]
    [InlineData("!start")]
    [InlineData("! start")]
    [InlineData(".start")]
    public async Task CommandAttribute_SupportsCustomPrefixes(string text)
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<CustomPrefixCommandHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate(text));

        Assert.Equal([$"custom-prefix:{text}"], probe.Events);
    }

    [Fact]
    public async Task CustomPrefixCommand_DoesNotTrimBotMention()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<CustomPrefixCommandHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("!start@botname"));

        Assert.Empty(probe.Events);
    }

    [Fact]
    public async Task SlashCommandMention_MatchesConfiguredCurrentBotOnly()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<BotMentionCommandHandler>(),
            options => options.BotUsername = "@teleflow_test_bot");

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/start"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/start@teleflow_test_bot"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/start@TELEFLOW_TEST_BOT"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/start@other_bot"));

        Assert.Equal(
            [
                "bot-mention:/start",
                "bot-mention:/start@teleflow_test_bot",
                "bot-mention:/start@TELEFLOW_TEST_BOT"
            ],
            probe.Events);
    }

    [Fact]
    public async Task SlashCommandMention_DoesNotMatchWhenBotIdentityIsUnknown()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<BotMentionCommandHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/start@teleflow_test_bot"));

        Assert.Empty(probe.Events);
    }

    [Fact]
    public async Task CommandAttribute_OverlappingPrefixes_UsesLongestPrefixRegardlessOfDeclarationOrder()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<ShortPrefixFirstCommandHandler>();
                services.AddTelegramHandler<LongPrefixFirstCommandHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("!!short-first"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("!!long-first"));

        Assert.Equal(["short-prefix-first", "long-prefix-first"], probe.Events);
    }

    [Fact]
    public async Task CommandAttribute_OptionalPrefix_MatchesPrefixedAndPrefixLessCommands()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<OptionalPrefixCommandHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/help"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("help"));

        Assert.Equal(["optional-prefix-command:/help", "optional-prefix-command:help"], probe.Events);
    }

    [Fact]
    public async Task CommandAttribute_OptionalPrefix_DoesNotMatchPrefixLessTextWithArguments()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<ShortOptionalPrefixCommandHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("Я рассказываю обычную фразу"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("я"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/я рассказываю обычную фразу"));

        Assert.Equal(
            [
                "message:Я рассказываю обычную фразу",
                "short-optional-prefix-command:я",
                "short-optional-prefix-command:/я рассказываю обычную фразу"
            ],
            probe.Events);
    }

    [Fact]
    public async Task CommandAttribute_NoPrefix_DoesNotMatchPrefixLessTextWithArguments()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<ShortNoPrefixCommandHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("Я рассказываю обычную фразу"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("я"));

        Assert.Equal(
            [
                "message:Я рассказываю обычную фразу",
                "short-no-prefix-command:я"
            ],
            probe.Events);
    }

    [Fact]
    public async Task TextTemplate_BindsRouteValue()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<TextTemplateRouteHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("order 922337203685477580"));

        Assert.Equal(["text-template:922337203685477580"], probe.Events);
    }

    [Fact]
    public async Task TextTemplate_OptionalRouteValue_MatchesMissingAndProvidedValues()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<OptionalTextTemplateRouteHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("order"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("order 123"));

        Assert.Equal(["optional-text-template:null", "optional-text-template:123"], probe.Events);
    }

    [Fact]
    public async Task TextTemplate_OptionalRouteValue_FallsThroughOnInvalidProvidedValue()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<OptionalTextTemplateRouteHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("order abc"));

        Assert.Equal(["message:order abc"], probe.Events);
    }

    [Fact]
    public async Task TextTemplate_RequiredRouteValue_DoesNotMatchMissingValue()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<TextTemplateRouteHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("order"));

        Assert.Equal(["message:order"], probe.Events);
    }

    [Fact]
    public async Task TextTemplate_OptionalRouteValue_DoesNotMakeLiteralSegmentOptional()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<OptionalLiteralTextTemplateRouteHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("Ada"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello Ada"));

        Assert.Equal(["message:Ada", "optional-literal:Ada"], probe.Events);
    }

    [Fact]
    public async Task CommandTemplate_BindsRouteValue()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<CommandTemplateRouteHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/ban 42"));

        Assert.Equal(["command-template:42"], probe.Events);
    }

    [Fact]
    public async Task CommandTemplate_OptionalPrefix_MatchesPrefixedAndPrefixLessCommands()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<OptionalPrefixCommandTemplateRouteHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/ban 42"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("ban 43"));

        Assert.Equal(["optional-prefix-command-template:42", "optional-prefix-command-template:43"], probe.Events);
    }

    [Fact]
    public async Task CommandTemplate_OptionalPrefix_DoesNotMatchPrefixLessTextWhenTemplateHasNoRouteValues()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<ShortOptionalPrefixCommandTemplateHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("Я рассказываю обычную фразу"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("я"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/я рассказываю обычную фразу"));

        Assert.Equal(
            [
                "message:Я рассказываю обычную фразу",
                "short-optional-prefix-command-template:я",
                "message:/я рассказываю обычную фразу"
            ],
            probe.Events);
    }

    [Fact]
    public async Task CommandTemplate_NoPrefix_MatchesOnlyPrefixLessCommands()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<NoPrefixCommandTemplateRouteHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("ban 42"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/ban 43"));

        Assert.Equal(["no-prefix-command-template:42", "message:/ban 43"], probe.Events);
    }

    [Fact]
    public async Task CommandTemplate_NoPrefix_DoesNotMatchPrefixLessTextWhenTemplateHasNoRouteValues()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<ShortNoPrefixCommandTemplateHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("Я рассказываю обычную фразу"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("я"));

        Assert.Equal(
            [
                "message:Я рассказываю обычную фразу",
                "short-no-prefix-command-template:я"
            ],
            probe.Events);
    }

    [Fact]
    public async Task CommandTemplate_OptionalPrefix_RespectsCustomPrefixes()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<OptionalCustomPrefixCommandTemplateRouteHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("!ban 42"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("ban 43"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/ban 44"));

        Assert.Equal(
            [
                "optional-custom-prefix-command-template:42",
                "optional-custom-prefix-command-template:43",
                "message:/ban 44"
            ],
            probe.Events);
    }

    [Fact]
    public async Task DeepLinkPayload_RoundTripsThroughStartCommandTemplate()
    {
        var deepLinks = new TelegramDeepLinks(
            "test_bot",
            new Base64UrlJsonDeepLinkPayloadSerializer());
        var payload = deepLinks.Serialize(new DispatcherDeepLinkPayload(42));
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<DeepLinkStartCommandHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate($"/start {payload}"));

        Assert.Equal(["deep-link:42"], probe.Events);
    }

    [Fact]
    public async Task CommandTemplate_OptionalRouteValue_MatchesMissingValue()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<OptionalCommandTemplateRouteHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/ban"));

        Assert.Equal(["optional-command-template:null"], probe.Events);
    }

    [Fact]
    public async Task TextRegex_BindsNamedGroup()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<TextRegexRouteHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("order 15"));

        Assert.Equal(["text-regex:15"], probe.Events);
    }

    [Fact]
    public async Task CommandRegex_RespectsCommandPrefixes()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<CommandRegexRouteHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("!kick 7"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/kick 8"));

        Assert.Equal(["command-regex:7"], probe.Events);
    }

    [Fact]
    public async Task CommandRegex_OptionalPrefix_MatchesPrefixedAndPrefixLessCommands()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<OptionalPrefixCommandRegexRouteHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/kick 7"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("kick 8"));

        Assert.Equal(["optional-prefix-command-regex:7", "optional-prefix-command-regex:8"], probe.Events);
    }

    [Fact]
    public void InvalidRouteTemplate_FailsClearly()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<InvalidTemplateRouteHandler>());

        Assert.Contains("unsupported placeholder constraint", exception.Message);
    }

    [Fact]
    public void NonCanonicalOptionalRouteTemplate_FailsClearly()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<NonCanonicalOptionalTemplateRouteHandler>());

        Assert.Contains("invalid placeholder", exception.Message);
    }

    [Fact]
    public void CommandTemplateWithPrefix_FailsClearly()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<PrefixedCommandTemplateRouteHandler>());

        Assert.Contains("must not include a command prefix", exception.Message);
    }

    [Fact]
    public void InvalidCommandPrefix_FailsClearly()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<InvalidCommandPrefixHandler>());

        Assert.Contains("prefixes", exception.Message);
    }

    [Fact]
    public void RouteValueWithoutParameter_FailsClearly()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<MissingRouteValueParameterHandler>());

        Assert.Contains("Route value 'id' must have a matching handler parameter", exception.Message);
    }

    [Fact]
    public void OptionalRouteValueRequiresNullableParameter()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<OptionalTemplateWithNonNullableParameterHandler>());

        Assert.Contains("must be nullable", exception.Message);
    }

    [Fact]
    public void RequiredRouteValueRejectsNullableParameter()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<RequiredTemplateWithNullableParameterHandler>());

        Assert.Contains("must be non-nullable", exception.Message);
    }

    [Fact]
    public async Task RoutePriority_PrefersExactTemplateThenRegex()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<TextRegexPriorityHandler>();
                services.AddTelegramHandler<TextTemplatePriorityHandler>();
                services.AddTelegramHandler<TextExactPriorityHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("order 42"));

        Assert.Equal(["priority:exact"], probe.Events);
    }

    [Fact]
    public async Task RoutePriority_PrefersConstrainedTemplateOverGenericTemplate()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<GenericTextTemplatePriorityHandler>();
                services.AddTelegramHandler<ConstrainedTextTemplatePriorityHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("order 42"));

        Assert.Equal(["priority:constrained-template:42"], probe.Events);
    }

    [Fact]
    public async Task RoutePriority_PrefersRequiredTemplateOverOptionalTemplate()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<OptionalTextTemplatePriorityHandler>();
                services.AddTelegramHandler<RequiredTextTemplatePriorityHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("item 42"));

        Assert.Equal(["priority:required-template:42"], probe.Events);
    }

    [Fact]
    public async Task RoutePriority_PrefersConstrainedCommandTemplateOverGenericTemplate()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<GenericCommandTemplatePriorityHandler>();
                services.AddTelegramHandler<ConstrainedCommandTemplatePriorityHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/ban 42"));

        Assert.Equal(["priority:constrained-command-template:42"], probe.Events);
    }

    [Fact]
    public async Task TextRegex_MissingOptionalRouteValue_FallsThrough()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<OptionalRegexRouteValueHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("order"));

        Assert.Equal(["message:order"], probe.Events);
    }

    [Fact]
    public async Task TextRegex_NonParsableRouteValue_FallsThrough()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<NonParsableRegexRouteValueHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("order abc"));

        Assert.Equal(["message:order abc"], probe.Events);
    }

    [Fact]
    public async Task ChatTypeFilter_MatchesExpectedChatType()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<PrivateChatHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello", chatType: "group"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello", chatType: "private"));

        Assert.Equal(["message:hello", "private-chat:hello"], probe.Events);
    }

    [Fact]
    public async Task ChatIdentityFilters_MatchMessageChat()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<ChatUsernameHandler>();
                services.AddTelegramHandler<ChatIdHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("wrong", message => message with
        {
            Chat = new Chat { Id = 200, Type = "private", Username = "other" }
        }));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("by-id"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("by-username", message => message with
        {
            Chat = new Chat { Id = 200, Type = "private", Username = "Group" }
        }));

        Assert.Equal(["message:wrong", "chat-id:by-id", "chat-username:by-username"], probe.Events);
    }

    [Fact]
    public async Task ChatFilters_RequireAccessibleCallbackMessage()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<CallbackChatIdHandler>();
                services.AddTelegramHandler<CallbackChatUsernameHandler>();
                services.AddTelegramHandler<RawCallbackHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("no-message"));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate("accessible", includeMessage: true));
        await DispatchAsync(
            serviceProvider,
            CreateCallbackUpdate(
                "accessible-username",
                includeMessage: true,
                configureMessage: message => message with
                {
                    Chat = new Chat { Id = 200, Type = "private", Username = "Group" }
                }));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate("inaccessible", inaccessibleChatId: 100));
        await DispatchAsync(
            serviceProvider,
            CreateCallbackUpdate(
                "inaccessible-username",
                configureInaccessibleMessage: message => message with
                {
                    Chat = new Chat { Id = 200, Type = "private", Username = "Group" }
                }));

        Assert.Equal(
            [
                "callback:no-message",
                "callback-chat-id:accessible",
                "callback-chat-username:accessible-username",
                "callback:inaccessible",
                "callback:inaccessible-username"
            ],
            probe.Events);
    }

    [Fact]
    public async Task ChatTypeFilter_MatchesCallbackAccessibleMessage()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<CallbackPrivateChatTypeHandler>();
                services.AddTelegramHandler<RawCallbackHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("private", includeMessage: true));
        await DispatchAsync(
            serviceProvider,
            CreateCallbackUpdate("group", includeMessage: true, configureMessage: message => message with
            {
                Chat = new Chat { Id = 100, Type = "group" }
            }));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate("inaccessible-private", inaccessibleChatId: 100));

        Assert.Equal(["callback-private-chat:private", "callback:group", "callback:inaccessible-private"], probe.Events);
    }

    [Fact]
    public async Task ChatFilter_MatchesChatMemberUpdateChat()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<ChatMemberChatIdHandler>();
                services.AddTelegramHandler<BroadChatMemberHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(
            serviceProvider,
            CreateChatMemberUpdate(
                isOwnUpdate: false,
                oldChatMember: LeftChatMember(),
                newChatMember: MemberChatMember()));

        Assert.Equal(["chat-member-chat-id:100"], probe.Events);
    }

    [Fact]
    public async Task MessageThreadFilters_MatchMessageAndCallbackThreads()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<TopicMessageHandler>();
                services.AddTelegramHandler<ThreadedCallbackHandler>();
                services.AddTelegramHandler<AnyThreadCallbackHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
                services.AddTelegramHandler<RawCallbackHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("plain"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("topic", message => message with { MessageThreadId = 42 }));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate("callback-plain", includeMessage: true));
        await DispatchAsync(
            serviceProvider,
            CreateCallbackUpdate("callback-topic", includeMessage: true, configureMessage: message => message with
            {
                MessageThreadId = 42
            }));
        await DispatchAsync(
            serviceProvider,
            CreateCallbackUpdate("callback-any-topic", includeMessage: true, configureMessage: message => message with
            {
                MessageThreadId = 77
            }));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate("callback-inaccessible-topic", inaccessibleChatId: 100));

        Assert.Equal(
            [
                "message:plain",
                "topic-message:topic",
                "callback:callback-plain",
                "threaded-callback:callback-topic",
                "has-thread-callback:callback-any-topic",
                "callback:callback-inaccessible-topic"
            ],
            probe.Events);
    }

    [Fact]
    public async Task HasMessageThreadFilter_MatchesAnyThreadedMessage()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<AnyThreadMessageHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("plain"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("topic", message => message with { MessageThreadId = 77 }));

        Assert.Equal(["message:plain", "has-thread:topic"], probe.Events);
    }

    [Fact]
    public async Task FromUserFilter_MatchesExpectedSender()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<FromUserHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello", fromUserId: 7));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello", fromUserId: 5));

        Assert.Equal(["message:hello", "from-user:5"], probe.Events);
    }

    [Fact]
    public async Task ContentFilters_MatchMessagePayloadShape()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<PhotoHandler>();
                services.AddTelegramHandler<DocumentHandler>();
                services.AddTelegramHandler<TextOnlyHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(
            serviceProvider,
            CreateMessageUpdate(null, message => message with { Photo = [CreatePhotoSize()] }));
        await DispatchAsync(
            serviceProvider,
            CreateMessageUpdate(null, message => message with { Document = CreateDocument() }));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello"));

        Assert.Equal(["photo", "document", "has-text:hello"], probe.Events);
    }

    [Fact]
    public async Task ReadyMadeContentFilters_MatchCatalogPayloadShapes()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<CaptionHandler>();
                services.AddTelegramHandler<VideoHandler>();
                services.AddTelegramHandler<AnimationHandler>();
                services.AddTelegramHandler<AudioHandler>();
                services.AddTelegramHandler<VoiceHandler>();
                services.AddTelegramHandler<VideoNoteHandler>();
                services.AddTelegramHandler<StickerHandler>();
                services.AddTelegramHandler<ContactHandler>();
                services.AddTelegramHandler<LocationHandler>();
                services.AddTelegramHandler<VenueHandler>();
                services.AddTelegramHandler<PollHandler>();
                services.AddTelegramHandler<DiceHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate(null));
        await DispatchAsync(serviceProvider, CreateMessageUpdate(null, message => message with { Caption = "caption" }));
        await DispatchAsync(serviceProvider, CreateMessageUpdate(null, message => message with { Video = CreateVideo() }));
        await DispatchAsync(serviceProvider, CreateMessageUpdate(null, message => message with { Animation = CreateAnimation() }));
        await DispatchAsync(serviceProvider, CreateMessageUpdate(null, message => message with { Audio = CreateAudio() }));
        await DispatchAsync(serviceProvider, CreateMessageUpdate(null, message => message with { Voice = CreateVoice() }));
        await DispatchAsync(serviceProvider, CreateMessageUpdate(null, message => message with { VideoNote = CreateVideoNote() }));
        await DispatchAsync(serviceProvider, CreateMessageUpdate(null, message => message with { Sticker = CreateSticker() }));
        await DispatchAsync(serviceProvider, CreateMessageUpdate(null, message => message with { Contact = CreateContact() }));
        await DispatchAsync(serviceProvider, CreateMessageUpdate(null, message => message with { Location = new Location { Latitude = 1, Longitude = 2 } }));
        await DispatchAsync(serviceProvider, CreateMessageUpdate(null, message => message with { Venue = CreateVenue() }));
        await DispatchAsync(serviceProvider, CreateMessageUpdate(null, message => message with { Poll = CreatePoll() }));
        await DispatchAsync(serviceProvider, CreateMessageUpdate(null, message => message with { Dice = CreateDice() }));

        Assert.Equal(
            [
                "message:",
                "caption:caption",
                "video",
                "animation",
                "audio",
                "voice",
                "video-note",
                "sticker",
                "contact",
                "location",
                "venue",
                "poll",
                "dice"
            ],
            probe.Events);
    }

    [Fact]
    public async Task SenderFilters_MatchBotAndPremiumSenders()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<BotSenderHandler>();
                services.AddTelegramHandler<PremiumSenderHandler>();
                services.AddTelegramHandler<HumanSenderHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(
            serviceProvider,
            CreateMessageUpdate("bot", message => message with
            {
                From = new User { Id = 10, IsBot = true, FirstName = "Bot" }
            }));
        await DispatchAsync(
            serviceProvider,
            CreateMessageUpdate("human", message => message with
            {
                From = new User { Id = 11, IsBot = false, FirstName = "Human" }
            }));
        await DispatchAsync(
            serviceProvider,
            CreateMessageUpdate("premium", message => message with
            {
                From = new User { Id = 12, IsBot = false, FirstName = "Premium", IsPremium = true }
            }));

        Assert.Equal(["from-bot", "from-human", "premium"], probe.Events);
    }

    [Fact]
    public async Task ReplyFilters_MatchReplyShape()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<ReplyToBotHandler>();
                services.AddTelegramHandler<ReplyHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("plain"));
        await DispatchAsync(
            serviceProvider,
            CreateMessageUpdate("reply-human", message => message with
            {
                ReplyToMessage = CreateReplyMessage(isBot: false)
            }));
        await DispatchAsync(
            serviceProvider,
            CreateMessageUpdate("reply-bot", message => message with
            {
                ReplyToMessage = CreateReplyMessage(isBot: true)
            }));

        Assert.Equal(["message:plain", "is-reply", "reply-to-bot"], probe.Events);
    }

    [Fact]
    public async Task RawCallbackFilters_MatchDataAndPrefix()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<AdminCallbackPrefixHandler>();
                services.AddTelegramHandler<HasCallbackDataHandler>();
                services.AddTelegramHandler<RawCallbackHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("admin:delete"));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate("user:open"));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate(null));

        Assert.Equal(["admin-callback:admin:delete", "has-callback-data:user:open", "callback:"], probe.Events);
    }

    [Fact]
    public async Task CallbackDataPrefix_TrimsPrefixInReflectionPath()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<TrimmedCallbackPrefixHandler>();
                services.AddTelegramHandler<RawCallbackHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("admin:delete"));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate(" admin:delete"));

        Assert.Equal(["trimmed-callback:admin:delete", "callback: admin:delete"], probe.Events);
    }

    [Fact]
    public async Task TypedCallbackPayloadMismatch_DoesNotEvaluateBuiltInCallbackFilters()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<TypedCallbackWithPrefixFilterHandler>();
                services.AddTelegramHandler<RawCallbackHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("del:not-json"));

        Assert.Equal(["callback:del:not-json"], probe.Events);
    }

    [Fact]
    public async Task CustomMessageFilter_AllowsMatchingHandler()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddSingleton<AllowMessageFilter>();
                services.AddTelegramHandler<CustomFilterMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("custom"));

        Assert.Equal(["custom-filter:custom"], probe.Events);
    }

    [Fact]
    public async Task ParameterizedCustomMessageFilter_ReceivesAttributeMetadata()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddSingleton<RequireMessageTextFilter>();
                services.AddTelegramHandler<ParameterizedCustomFilterMessageHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("CUSTOM"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("other"));

        Assert.Equal(["parameterized-filter:CUSTOM", "message:other"], probe.Events);
    }

    [Fact]
    public async Task CustomMessageFilter_FalseFallsThrough()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddSingleton<DenyMessageFilter>();
                services.AddTelegramHandler<DenyCustomFilterMessageHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("custom"));

        Assert.Equal(["message:custom"], probe.Events);
    }

    [Fact]
    public async Task FilterRejectedCandidate_LogsDebugDiagnosticWithoutMessageText()
    {
        var loggerFactory = new RecordingLoggerFactory();
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.AddSingleton<DenyMessageFilter>();
                services.AddTelegramHandler<DenyCustomFilterMessageHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("sensitive-message-text"));

        Assert.Equal(["message:sensitive-message-text"], probe.Events);
        var diagnostic = Assert.Single(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Debug &&
                     entry.EventId.Id == 2 &&
                     entry.Category == "TeleFlow.Telegram.Internal.Handlers.TelegramHandlerSelector");

        Assert.Contains("Telegram handler candidate rejected by filters", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("handler=DenyCustomFilterMessageHandler.Handle", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("route=MessageAny", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-message-text", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClassAndMethodCustomFilters_AreAndComposed()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddSingleton<AllowUpdateFilter>();
                services.AddSingleton<DenyMessageFilter>();
                services.AddTelegramHandler<AndCustomFilterMessageHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("custom"));

        Assert.Equal(["message:custom"], probe.Events);
    }

    [Fact]
    public async Task CustomCallbackFilter_AllowsMatchingHandler()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddSingleton<AllowCallbackFilter>();
                services.AddTelegramHandler<CustomFilterCallbackHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("raw"));

        Assert.Equal(["custom-callback:raw"], probe.Events);
    }

    [Fact]
    public async Task BuiltInFilterNoMatch_DoesNotResolveCustomFilter()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<BuiltInFilterBeforeCustomFilterHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("custom"));

        Assert.Equal(["message:custom"], probe.Events);
    }

    [Fact]
    public async Task TypedCallbackPayloadMismatch_DoesNotRunCustomFilter()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddSingleton<ThrowingCallbackFilter>();
                services.AddTelegramHandler<TypedCallbackWithThrowingFilterHandler>();
                services.AddTelegramHandler<RawCallbackHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("invalid"));

        Assert.Equal(["callback:invalid"], probe.Events);
    }

    [Fact]
    public async Task MissingCustomFilterService_FailsClearly()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<CustomFilterMessageHandler>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DispatchAsync(serviceProvider, CreateMessageUpdate("custom")));

        Assert.Contains(nameof(AllowMessageFilter), exception.Message);
    }

    [Fact]
    public async Task CustomFilterException_BubblesUnchanged()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddSingleton<ThrowingMessageFilter>();
                services.AddTelegramHandler<ThrowingCustomFilterMessageHandler>();
            });

        await Assert.ThrowsAsync<CustomFilterException>(
            () => DispatchAsync(serviceProvider, CreateMessageUpdate("custom")));
    }

    [Fact]
    public void IncompatibleCustomFilter_FailsRegistration()
    {
        var services = CreateBaseServices();
        services.AddSingleton<AllowMessageFilter>();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<CallbackWithMessageCustomFilterHandler>());

        Assert.Contains("not compatible", exception.Message);
    }

    [Fact]
    public async Task ChatMemberUpdatedHandler_DispatchesChatMemberUpdate()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<JoinChatMemberHandler>();
                services.AddTelegramHandler<BroadChatMemberHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(
            serviceProvider,
            CreateChatMemberUpdate(
                isOwnUpdate: false,
                oldChatMember: LeftChatMember(),
                newChatMember: MemberChatMember()));

        Assert.Equal(["join:5:100"], probe.Events);
    }

    [Fact]
    public void ReflectionAssemblyScanner_DiscoversChatMemberHandlers()
    {
        var scannerType = typeof(TelegramServiceCollectionExtensions).Assembly
            .GetType("TeleFlow.Telegram.Internal.Handlers.TelegramHandlerAssemblyScanner")
            ?? throw new InvalidOperationException("TelegramHandlerAssemblyScanner type was not found.");
        var method = scannerType.GetMethod(
            "GetHandlerTypes",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("TelegramHandlerAssemblyScanner.GetHandlerTypes was not found.");

        var handlerTypes = Assert.IsType<Type[]>(method.Invoke(null, [typeof(AssemblyScanChatMemberHandler).Assembly]));

        Assert.Contains(typeof(AssemblyScanChatMemberHandler), handlerTypes);
    }

    [Fact]
    public void ReflectionAssemblyScanner_DiscoversClassBasedHandlers()
    {
        var scannerType = typeof(TelegramServiceCollectionExtensions).Assembly
            .GetType("TeleFlow.Telegram.Internal.Handlers.TelegramHandlerAssemblyScanner")
            ?? throw new InvalidOperationException("TelegramHandlerAssemblyScanner type was not found.");
        var method = scannerType.GetMethod(
            "GetHandlerTypes",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("TelegramHandlerAssemblyScanner.GetHandlerTypes was not found.");

        var handlerTypes = Assert.IsType<Type[]>(method.Invoke(null, [typeof(ClassBasedStartHandler).Assembly]));

        Assert.Contains(typeof(ClassBasedStartHandler), handlerTypes);
    }

    [Fact]
    public void AddTelegramHandlersFromAssembly_RequiresGeneratedHandlerMetadata()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandlersFromAssembly(typeof(TelegramServiceCollectionExtensions).Assembly));

        Assert.Contains("generated Telegram handler metadata", exception.Message);
        Assert.Contains("AddTelegramHandler<THandler>", exception.Message);
    }

    [Fact]
    public async Task AddTelegramHandler_RegistersOnlyExplicitHandlerType()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<DirectOnlyCommandHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/direct-only"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/direct-sibling"));

        Assert.Equal(["direct-only:/direct-only"], probe.Events);
    }

    [Fact]
    public async Task MyChatMemberUpdatedHandler_DispatchesMyChatMemberUpdate()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<MyChatMemberChangedHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(
            serviceProvider,
            CreateChatMemberUpdate(
                isOwnUpdate: true,
                oldChatMember: BannedChatMember(),
                newChatMember: MemberChatMember()));

        Assert.Equal(["my-chat-member:5:100"], probe.Events);
    }

    [Fact]
    public async Task ChatMemberTransition_DistinguishesRestrictedMemberAndRestrictedNotMember()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<JoinChatMemberHandler>();
                services.AddTelegramHandler<BroadChatMemberHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(
            serviceProvider,
            CreateChatMemberUpdate(
                isOwnUpdate: false,
                oldChatMember: RestrictedChatMember(isMember: false),
                newChatMember: MemberChatMember()));
        await DispatchAsync(
            serviceProvider,
            CreateChatMemberUpdate(
                isOwnUpdate: false,
                oldChatMember: RestrictedChatMember(isMember: true),
                newChatMember: MemberChatMember()));

        Assert.Equal(["join:5:100", "chat-member:5:100"], probe.Events);
    }

    [Fact]
    public async Task ChatMemberTransition_MatchesPromotedAndDemoted()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<PromotedChatMemberHandler>();
                services.AddTelegramHandler<DemotedChatMemberHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(
            serviceProvider,
            CreateChatMemberUpdate(
                isOwnUpdate: false,
                oldChatMember: MemberChatMember(),
                newChatMember: AdministratorChatMember()));
        await DispatchAsync(
            serviceProvider,
            CreateChatMemberUpdate(
                isOwnUpdate: false,
                oldChatMember: AdministratorChatMember(),
                newChatMember: MemberChatMember()));

        Assert.Equal(["promoted:5", "demoted:5"], probe.Events);
    }

    [Fact]
    public async Task RequireTelegramRole_MatchesMessageWhenCurrentUserHasAllowedRole()
    {
        var resolver = new RecordingTelegramRoleResolver(TelegramMemberStatusSet.Administrator);
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ITelegramChatMemberStatusResolver>();
                services.AddSingleton<ITelegramChatMemberStatusResolver>(resolver);
                services.AddTelegramHandler<AdminCommandRoleHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/admin"));

        Assert.Equal(["role-command:/admin"], probe.Events);
        Assert.Equal([(100L, 5L)], resolver.Requests);
    }

    [Fact]
    public async Task RequireTelegramRole_IsRouteNoMatchWhenCurrentUserDoesNotHaveRole()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ITelegramChatMemberStatusResolver>();
                services.AddSingleton<ITelegramChatMemberStatusResolver>(
                    new RecordingTelegramRoleResolver(TelegramMemberStatusSet.Member));
                services.AddTelegramHandler<AdminCommandRoleHandler>();
                services.AddTelegramHandler<RoleFallbackMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/admin"));

        Assert.Equal(["role-fallback:/admin"], probe.Events);
    }

    [Fact]
    public async Task RequireTelegramRole_ComposesTypeAndMethodRequirementsWithAnd()
    {
        using var administratorProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ITelegramChatMemberStatusResolver>();
                services.AddSingleton<ITelegramChatMemberStatusResolver>(
                    new RecordingTelegramRoleResolver(TelegramMemberStatusSet.Administrator));
                services.AddTelegramHandler<AdminAndCreatorRoleHandler>();
                services.AddTelegramHandler<RoleFallbackMessageHandler>();
            });
        var administratorProbe = administratorProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(administratorProvider, CreateMessageUpdate("secure"));

        Assert.Equal(["role-fallback:secure"], administratorProbe.Events);

        using var creatorProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ITelegramChatMemberStatusResolver>();
                services.AddSingleton<ITelegramChatMemberStatusResolver>(
                    new RecordingTelegramRoleResolver(TelegramMemberStatusSet.Creator));
                services.AddTelegramHandler<AdminAndCreatorRoleHandler>();
                services.AddTelegramHandler<RoleFallbackMessageHandler>();
            });
        var creatorProbe = creatorProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(creatorProvider, CreateMessageUpdate("secure"));

        Assert.Equal(["role-admin-creator"], creatorProbe.Events);
    }

    [Fact]
    public async Task RequireTelegramRole_MatchesCallbackOnlyWhenCallbackHasAccessibleMessage()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ITelegramChatMemberStatusResolver>();
                services.AddSingleton<ITelegramChatMemberStatusResolver>(
                    new RecordingTelegramRoleResolver(TelegramMemberStatusSet.Administrator));
                services.AddTelegramHandler<AdminCallbackRoleHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("role", includeMessage: false));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate("role", includeMessage: true));

        Assert.Equal(["role-callback:role"], probe.Events);
    }

    [Fact]
    public async Task RequireTelegramRole_UsesChatMemberActorIdentity()
    {
        var resolver = new RecordingTelegramRoleResolver(TelegramMemberStatusSet.Administrator);
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ITelegramChatMemberStatusResolver>();
                services.AddSingleton<ITelegramChatMemberStatusResolver>(resolver);
                services.AddTelegramHandler<AdminChatMemberRoleHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(
            serviceProvider,
            CreateChatMemberUpdate(
                isOwnUpdate: false,
                oldChatMember: LeftChatMember(),
                newChatMember: MemberChatMember()));

        Assert.Equal(["role-chat-member:42:5"], probe.Events);
        Assert.Equal([(100L, 42L)], resolver.Requests);
    }

    [Fact]
    public async Task RequireTelegramRole_CachesResolvedStatusesByChatAndUser()
    {
        var resolver = new RecordingTelegramRoleResolver(TelegramMemberStatusSet.Administrator);
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ITelegramChatMemberStatusResolver>();
                services.AddSingleton<ITelegramChatMemberStatusResolver>(resolver);
                services.AddTelegramHandler<AdminCommandRoleHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/admin"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/admin"));

        Assert.Equal(["role-command:/admin", "role-command:/admin"], probe.Events);
        Assert.Single(resolver.Requests);
    }

    [Fact]
    public async Task RequireTelegramRole_DoesNotCacheResolvedStatusesWhenCacheIsDisabled()
    {
        var resolver = new RecordingTelegramRoleResolver(TelegramMemberStatusSet.Administrator);
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ITelegramChatMemberStatusResolver>();
                services.AddSingleton<ITelegramChatMemberStatusResolver>(resolver);
                services.RemoveAll<TelegramRoleFilterOptions>();
                services.AddSingleton(new TelegramRoleFilterOptions { CacheEnabled = false });
                services.AddTelegramHandler<AdminCommandRoleHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/admin"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/admin"));

        Assert.Equal(["role-command:/admin", "role-command:/admin"], probe.Events);
        Assert.Equal([(100L, 5L), (100L, 5L)], resolver.Requests);
    }

    [Fact]
    public async Task RequireTelegramRole_DoesNotResolveRoleAfterCustomFilterNoMatch()
    {
        var resolver = new RecordingTelegramRoleResolver(TelegramMemberStatusSet.Administrator);
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ITelegramChatMemberStatusResolver>();
                services.AddSingleton<ITelegramChatMemberStatusResolver>(resolver);
                services.AddSingleton<DenyMessageFilter>();
                services.AddTelegramHandler<DenyCustomFilterRoleHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("role-filter"));

        Assert.Empty(probe.Events);
        Assert.Empty(resolver.Requests);
    }

    [Fact]
    public async Task RequireTelegramRole_CanComposeWithAllowingCustomFilter()
    {
        var resolver = new RecordingTelegramRoleResolver(TelegramMemberStatusSet.Administrator);
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ITelegramChatMemberStatusResolver>();
                services.AddSingleton<ITelegramChatMemberStatusResolver>(resolver);
                services.AddSingleton<AllowMessageFilter>();
                services.AddTelegramHandler<AllowCustomFilterRoleHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("role-filter"));

        Assert.Equal(["allow-role-filter:role-filter"], probe.Events);
        Assert.Equal([(100L, 5L)], resolver.Requests);
    }

    [Fact]
    public async Task TypeAndMethodFilters_ComposeWithAnd()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<PrivateTextHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate(null));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello", chatType: "group"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello", chatType: "private"));

        Assert.Equal(["message:", "message:hello", "private-text:hello"], probe.Events);
    }

    [Fact]
    public async Task InheritedClassLevelFilters_MatchReflectionPath()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<InheritedPrivateTextHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello", chatType: "group"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello", chatType: "private"));

        Assert.Equal(["message:hello", "inherited-private-text:hello"], probe.Events);
    }

    [Fact]
    public void InheritedHandlerMethodWithoutOverride_FailsClearly()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<InheritedMessageHandlerWithoutOverride>());

        Assert.Contains("inherits handler methods", exception.Message);
        Assert.Contains("must be overridden", exception.Message);
    }

    [Fact]
    public void OwnHandlerPlusInheritedHandlerMethodWithoutOverride_FailsClearly()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<OwnAndInheritedMessageHandlerWithoutOverride>());

        Assert.Contains("inherits handler methods", exception.Message);
        Assert.Contains("must be overridden", exception.Message);
    }

    [Fact]
    public async Task OverrideHandlerMethod_InheritsRouteMetadata()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<OverrideInheritedMessageHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello"));

        Assert.Equal(["override-inherited:hello"], probe.Events);
    }

    [Fact]
    public void CallbackHandlerWithMessageFilter_FailsClearly()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<CallbackWithMessageFilterHandler>());

        Assert.Contains("Message filters cannot be used on callback handlers", exception.Message);
    }

    [Fact]
    public async Task CallbackHandler_DispatchesRawCallbackQueries()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<RawCallbackHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("raw"));

        Assert.Equal(["callback:raw"], probe.Events);
    }

    [Fact]
    public async Task RawCallbackHandler_ResolvesServiceParameters()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<LateServiceCallbackHandler>();
                services.AddSingleton<LateCallbackService>();
            });

        var service = serviceProvider.GetRequiredService<LateCallbackService>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("raw"));

        Assert.Equal(["late-service:raw"], service.Events);
    }

    [Fact]
    public void RawCallbackHandler_WithCallbackDataPayload_FailsClearly()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<RawCallbackWithPayloadHandler>());

        Assert.Contains("Raw CallbackAttribute handlers do not bind typed callback payloads", exception.Message);
        Assert.Contains("Use CallbackAttribute<TPayload>", exception.Message);
    }

    [Fact]
    public async Task TypedCallbackHandler_ReceivesDeserializedPayload()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<TypedCallbackHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("""{"id":42}"""));

        Assert.Equal(["typed-callback:42"], probe.Events);
    }

    [Fact]
    public async Task CompactCallbackHandler_ReceivesPrefixedPayload()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<CompactCallbackHandler>());
        var serializer = serviceProvider.GetRequiredService<ICallbackDataSerializer>();
        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        var callbackData = serializer.Serialize(new CompactDeleteCallback("a:b%z", 42));

        await DispatchAsync(serviceProvider, CreateCallbackUpdate(callbackData));

        Assert.Equal("del:a%3Ab%25z:42", callbackData);
        Assert.Equal(["compact-callback:a:b%z:42"], probe.Events);
    }

    [Fact]
    public async Task InlineKeyboardBuilder_CompactCallbackData_DispatchesToTypedCallbackHandler()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<CompactCallbackHandler>());
        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        var keyboard = InlineKeyboardBuilder.Create()
            .Button("Delete", new CompactDeleteCallback("x", 7))
            .Build();
        var callbackData = keyboard.InlineKeyboard[0][0].CallbackData;

        await DispatchAsync(serviceProvider, CreateCallbackUpdate(callbackData));

        Assert.Equal("del:x:7", callbackData);
        Assert.Equal(["compact-callback:x:7"], probe.Events);
    }

    [Fact]
    public async Task TypedCallbackHandler_RunsBeforeRawFallback()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<RawCallbackHandler>();
                services.AddTelegramHandler<CompactCallbackHandler>();
            });
        var serializer = serviceProvider.GetRequiredService<ICallbackDataSerializer>();
        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(
            serviceProvider,
            CreateCallbackUpdate(serializer.Serialize(new CompactDeleteCallback("x", 7))));

        Assert.Equal(["compact-callback:x:7"], probe.Events);
    }

    [Fact]
    public async Task CompactCallbackPrefixMismatch_DoesNotMatchTypedHandler()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<CompactCallbackHandler>();
                services.AddTelegramHandler<RawCallbackHandler>();
            });
        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("other:value"));

        Assert.Equal(["callback:other:value"], probe.Events);
    }

    [Fact]
    public async Task TypedCallbackRouteDeserializer_RejectedPayload_DoesNotBindPayload()
    {
        var serializer = new RouteDeserializingCallbackDataSerializer(canDeserialize: false);
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddSingleton<ICallbackDataSerializer>(serializer);
                services.AddTelegramHandler<CompactCallbackHandler>();
                services.AddTelegramHandler<RawCallbackHandler>();
            });
        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("other:value"));

        Assert.Equal(["callback:other:value"], probe.Events);
        Assert.Equal(1, serializer.RouteDeserializeCalls);
        Assert.Equal(0, serializer.PayloadBindingCalls);
        Assert.Equal(0, serializer.PublicDeserializeCalls);
    }

    [Fact]
    public async Task TypedCallbackRouteDeserializer_AcceptedPayload_BindsPayload()
    {
        var serializer = new RouteDeserializingCallbackDataSerializer(canDeserialize: true);
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddSingleton<ICallbackDataSerializer>(serializer);
                services.AddTelegramHandler<CompactCallbackHandler>();
                services.AddTelegramHandler<RawCallbackHandler>();
            });
        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("del:item:9"));

        Assert.Equal(["compact-callback:item:9"], probe.Events);
        Assert.Equal(1, serializer.RouteDeserializeCalls);
        Assert.Equal(1, serializer.PayloadBindingCalls);
        Assert.Equal(0, serializer.PublicDeserializeCalls);
    }

    [Fact]
    public void DefaultCallbackRouteDeserializer_RejectsCompactPayloadByPrefixAndShape()
    {
        using var serviceProvider = CreateServiceProvider(static _ => { });
        var serializer = serviceProvider.GetRequiredService<ICallbackDataSerializer>();
        var routeDeserializer = Assert.IsAssignableFrom<ICallbackDataRouteDeserializer>(serializer);

        Assert.False(routeDeserializer.TryDeserializeForRoute(typeof(CompactDeleteCallback), "other:value", out _));
        Assert.False(routeDeserializer.TryDeserializeForRoute(typeof(CompactDeleteCallback), "delete:x:7", out _));
        Assert.False(routeDeserializer.TryDeserializeForRoute(typeof(CompactDeleteCallback), "del:x", out _));
        Assert.False(routeDeserializer.TryDeserializeForRoute(typeof(CompactDeleteCallback), "del:x:7:extra", out _));

        Assert.True(routeDeserializer.TryDeserializeForRoute(typeof(CompactDeleteCallback), "del:x:7", out var payload));
        var callbackPayload = Assert.IsType<CompactDeleteCallback>(payload);

        Assert.Equal("x", callbackPayload.Name);
        Assert.Equal(7, callbackPayload.Id);
    }

    [Fact]
    public void DefaultCallbackRouteDeserializer_PreservesJsonPayloadFallback()
    {
        using var serviceProvider = CreateServiceProvider(static _ => { });
        var serializer = serviceProvider.GetRequiredService<ICallbackDataSerializer>();
        var routeDeserializer = Assert.IsAssignableFrom<ICallbackDataRouteDeserializer>(serializer);

        Assert.True(routeDeserializer.TryDeserializeForRoute(typeof(DeleteCallbackPayload), """{"id":42}""", out var payload));
        var callbackPayload = Assert.IsType<DeleteCallbackPayload>(payload);

        Assert.Equal(42, callbackPayload.Id);
    }

    [Fact]
    public void DuplicateCompactCallbackPrefixes_FailDuringDispatcherBuild()
    {
        var services = CreateBaseServices();
        services.AddTelegramHandler<CompactCallbackHandler>();
        services.AddTelegramHandler<DuplicateCompactCallbackHandler>();

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<TeleFlow.Framework.Dispatching.IUpdateDispatcher>());

        Assert.Contains("Duplicate Telegram callback data prefix 'del'", exception.Message);
    }

    [Fact]
    public async Task InvalidTypedCallbackData_DoesNotMatchTypedHandler()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddTelegramHandler<TypedCallbackHandler>();
                services.AddTelegramHandler<RawCallbackHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("not-json"));

        Assert.Equal(["callback:not-json"], probe.Events);
    }

    [Fact]
    public async Task MalformedCompactCallbackData_LogsDiagnosticAndFallsBackToRawCallback()
    {
        var loggerFactory = new RecordingLoggerFactory();
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(loggerFactory);
                services.AddTelegramHandler<CompactCallbackHandler>();
                services.AddTelegramHandler<RawCallbackHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("del:item:not-int"));

        Assert.Equal(["callback:del:item:not-int"], probe.Events);
        var warning = Assert.Single(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Warning &&
                     entry.Category == "TeleFlow.Telegram.Internal.Handlers.TelegramHandlerSelector");
        Assert.Contains("Telegram callback data failed to deserialize", warning.Message);
        Assert.Equal(1, warning.EventId.Id);
        Assert.Contains(nameof(CompactDeleteCallback), warning.Message);
        Assert.NotNull(warning.Exception);
    }

    [Fact]
    public async Task BrokenCustomCallbackSerializer_ExceptionBubbles()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddSingleton<ICallbackDataSerializer, BrokenCallbackDataSerializer>();
                services.AddTelegramHandler<TypedCallbackHandler>();
                services.AddTelegramHandler<RawCallbackHandler>();
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DispatchAsync(serviceProvider, CreateCallbackUpdate("broken")));

        Assert.Contains("Broken callback serializer", exception.Message);
    }

    [Fact]
    public async Task CustomCallbackSerializer_InvalidPayloadExceptionDoesNotMatchTypedHandler()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddSingleton<ICallbackDataSerializer, InvalidPayloadCallbackDataSerializer>();
                services.AddTelegramHandler<TypedCallbackHandler>();
                services.AddTelegramHandler<RawCallbackHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("invalid"));

        Assert.Equal(["callback:invalid"], probe.Events);
    }

    [Fact]
    public async Task CustomCallbackSerializer_CancellationBubbles()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddSingleton<ICallbackDataSerializer, CancelingCallbackDataSerializer>();
                services.AddTelegramHandler<TypedCallbackHandler>();
                services.AddTelegramHandler<RawCallbackHandler>();
            });

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => DispatchAsync(serviceProvider, CreateCallbackUpdate("cancel")));
    }

    [Fact]
    public async Task AutoAnswerCallbackAttribute_AnswersAfterSuccessfulCallbackHandler()
    {
        var transport = new RecordingTelegramTransport(CreateOkResponse());
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ITelegramTransport>();
                services.AddSingleton<ITelegramTransport>(transport);
                services.AddTelegramHandler<AutoAnswerCallbackHandler>();
            });
        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("auto"));

        Assert.Equal(["auto-answer:auto"], probe.Events);
        var request = Assert.Single(transport.Requests);
        Assert.Equal("answerCallbackQuery", request.MethodName);
        Assert.Contains("\"callback_query_id\":\"cb\"", request.Json);
        Assert.Contains("\"text\":\"Deleted\"", request.Json);
        Assert.DoesNotContain("\"show_alert\"", request.Json);
    }

    [Fact]
    public async Task AddAutoCallbackAnswer_AnswersAllSuccessfulCallbackHandlers()
    {
        var transport = new RecordingTelegramTransport(CreateOkResponse());
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddAutoCallbackAnswer(options =>
                {
                    options.Text = "OK";
                    options.ShowAlert = true;
                });
                services.RemoveAll<ITelegramTransport>();
                services.AddSingleton<ITelegramTransport>(transport);
                services.AddTelegramHandler<RawCallbackHandler>();
            });

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("raw"));

        var request = Assert.Single(transport.Requests);
        Assert.Equal("answerCallbackQuery", request.MethodName);
        Assert.Contains("\"text\":\"OK\"", request.Json);
        Assert.Contains("\"show_alert\":true", request.Json);
    }

    [Fact]
    public async Task AutoAnswerCallbackAttribute_OverridesGlobalDefault()
    {
        var transport = new RecordingTelegramTransport(CreateOkResponse());
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddAutoCallbackAnswer(options => options.Text = "Global");
                services.RemoveAll<ITelegramTransport>();
                services.AddSingleton<ITelegramTransport>(transport);
                services.AddTelegramHandler<AutoAnswerCallbackHandler>();
            });

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("auto"));

        Assert.Contains("\"text\":\"Deleted\"", Assert.Single(transport.Requests).Json);
    }

    [Fact]
    public async Task AutoAnswerCallbackAttribute_CanDisableGlobalDefault()
    {
        var transport = new RecordingTelegramTransport(CreateOkResponse());
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddAutoCallbackAnswer(options => options.Text = "Global");
                services.RemoveAll<ITelegramTransport>();
                services.AddSingleton<ITelegramTransport>(transport);
                services.AddTelegramHandler<DisabledAutoAnswerCallbackHandler>();
            });

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("disabled"));

        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task AutoAnswerCallback_DoesNotDuplicateManualCallbackAnswer()
    {
        var transport = new RecordingTelegramTransport(CreateOkResponse());
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddAutoCallbackAnswer(options => options.Text = "Global");
                services.RemoveAll<ITelegramTransport>();
                services.AddSingleton<ITelegramTransport>(transport);
                services.AddTelegramHandler<ManualAnswerCallbackHandler>();
            });

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("manual"));

        var request = Assert.Single(transport.Requests);
        Assert.Equal("answerCallbackQuery", request.MethodName);
        Assert.Contains("\"text\":\"Manual\"", request.Json);
        Assert.DoesNotContain("Global", request.Json);
    }

    [Fact]
    public async Task AutoAnswerCallback_DoesNotAnswerWhenHandlerFails()
    {
        var transport = new RecordingTelegramTransport(CreateOkResponse());
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddAutoCallbackAnswer();
                services.RemoveAll<ITelegramTransport>();
                services.AddSingleton<ITelegramTransport>(transport);
                services.AddTelegramHandler<ThrowingAutoAnswerCallbackHandler>();
            });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => DispatchAsync(serviceProvider, CreateCallbackUpdate("boom")));

        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task AutoAnswerCallback_FailureUsesTelegramErrorHandlers()
    {
        var transport = new RecordingTelegramTransport(new TelegramTransportResponse(
            500,
            """{"ok":false,"error_code":500,"description":"Internal Server Error"}"""));
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ITelegramTransport>();
                services.AddSingleton<ITelegramTransport>(transport);
                services.AddTelegramHandler<AutoAnswerCallbackHandler>();
                services.AddTelegramHandler<AutoAnswerFailureErrorHandler>();
            });
        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateCallbackUpdate("auto"));

        Assert.Equal(
            ["auto-answer:auto", "auto-answer-error:TelegramServerException"],
            probe.Events);
    }

    [Fact]
    public async Task AutoAnswerCallback_UnhandledFailureRethrows()
    {
        var transport = new RecordingTelegramTransport(new TelegramTransportResponse(
            500,
            """{"ok":false,"error_code":500,"description":"Internal Server Error"}"""));
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.RemoveAll<ITelegramTransport>();
                services.AddSingleton<ITelegramTransport>(transport);
                services.AddTelegramHandler<AutoAnswerCallbackHandler>();
            });
        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        var exception = await Assert.ThrowsAsync<TelegramServerException>(
            () => DispatchAsync(serviceProvider, CreateCallbackUpdate("auto")));

        Assert.Equal("answerCallbackQuery", exception.MethodName);
        Assert.Equal(["auto-answer:auto"], probe.Events);
    }

    [Fact]
    public async Task AutoAnswerCallback_DoesNotRunForMessageHandlers()
    {
        var transport = new RecordingTelegramTransport(CreateOkResponse());
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddAutoCallbackAnswer();
                services.RemoveAll<ITelegramTransport>();
                services.AddSingleton<ITelegramTransport>(transport);
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        await DispatchAsync(serviceProvider, CreateMessageUpdate("hello"));

        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task HandlerException_BubblesThroughRunAsyncUnchanged()
    {
        var expected = new InvalidOperationException("handler failed");
        ThrowingMessageHandler.Exception = expected;

        var builder = TeleFlowApplication.CreateBuilder();
        builder.Services.AddTelegramBot(options => options.Token = "test-token");
        builder.Services.AddSingleton<HandlerProbe>();
        builder.Services.AddTelegramHandler<ThrowingMessageHandler>();
        builder.Services.AddSingleton<IUpdateSource>(
            new SinglePayloadUpdateSource(new TelegramUpdatePayload(CreateMessageUpdate("boom"))));

        await using var application = builder.Build();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => application.RunAsync());

        Assert.Same(expected, exception);
    }

    [Fact]
    public async Task NoMatchingHandler_IsNoOp()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramHandler<StartCommandHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("plain text"));

        Assert.Empty(probe.Events);
    }

    [Fact]
    public void InvalidHandlerSignatures_FailDuringRegistration()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<InvalidReturnHandler>());

        Assert.Contains("Task or ValueTask", exception.Message);
    }

    [Fact]
    public void DuplicateExactCommandHandlers_FailDuringRegistration()
    {
        var services = CreateBaseServices();

        services.AddTelegramHandler<StartCommandHandler>();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<DuplicateStartCommandHandler>());

        Assert.Contains("Duplicate Telegram command", exception.Message);
    }

    [Fact]
    public async Task AddTelegramModule_DispatchesModuleCommandHandler()
    {
        using var serviceProvider = CreateServiceProvider(
            services => services.AddTelegramModule<AdminModuleHandler>());

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/admin"));

        Assert.Equal(["module:/admin"], probe.Events);
    }

    [Fact]
    public void AddTelegramModule_RecordsModuleName()
    {
        var services = CreateBaseServices();

        services.AddTelegramModule<AdminModuleHandler>();

        Assert.Equal(["admin"], GetRegisteredModuleNames(services));
    }

    [Fact]
    public void StandaloneHandlerDescriptor_HasNoModuleName()
    {
        var services = CreateBaseServices();

        services.AddTelegramHandler<StartCommandHandler>();

        Assert.Equal([null], GetRegisteredModuleNames(services));
    }

    [Fact]
    public void AddTelegramHandler_PreservesModuleName_WhenHandlerTypeIsModule()
    {
        var services = CreateBaseServices();

        services.AddTelegramHandler<AdminModuleHandler>();

        Assert.Equal(["admin"], GetRegisteredModuleNames(services));
    }

    [Fact]
    public void AddTelegramModule_FailsWithoutTelegramModuleAttribute()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramModule<NonModuleCommandHandler>());

        Assert.Contains(nameof(TelegramModuleAttribute), exception.Message);
    }

    [Fact]
    public void DuplicateCommandsAcrossModuleAndStandaloneHandlers_FailDuringRegistration()
    {
        var services = CreateBaseServices();

        services.AddTelegramHandler<StartCommandHandler>();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramModule<DuplicateStartModuleHandler>());

        Assert.Contains("Duplicate Telegram command", exception.Message);
    }

    [Fact]
    public void AddTelegramModule_DoesNotReplaceCustomDispatcher()
    {
        var services = CreateBaseServices();
        var dispatcher = new ModuleCustomDispatcher();

        services.AddSingleton<TeleFlow.Framework.Dispatching.IUpdateDispatcher>(dispatcher);
        services.AddTelegramModule<AdminModuleHandler>();

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
    public void DefaultCallbackSerializer_EnforcesTelegramByteLimit()
    {
        using var serviceProvider = CreateServiceProvider(static _ => { });
        var serializer = serviceProvider.GetRequiredService<ICallbackDataSerializer>();

        var exception = Assert.Throws<InvalidOperationException>(
            () => serializer.Serialize(new LargeCallbackPayload(new string('x', 80))));

        Assert.Contains("64 UTF-8 bytes", exception.Message);
    }

    [Fact]
    public void CallbackDataMetadata_TryCreate_CachesCompactPayloadMetadata()
    {
        Assert.True(CallbackDataMetadata.TryCreate(typeof(CompactDeleteCallback), out var first));
        Assert.True(CallbackDataMetadata.TryCreate(typeof(CompactDeleteCallback), out var second));

        Assert.Same(first, second);
    }

    [Fact]
    public void CallbackDataMetadata_TryCreate_PreservesJsonFallbackForNonCompactPayloads()
    {
        using var serviceProvider = CreateServiceProvider(static _ => { });
        var serializer = serviceProvider.GetRequiredService<ICallbackDataSerializer>();

        Assert.False(CallbackDataMetadata.TryCreate(typeof(DeleteCallbackPayload), out _));
        Assert.False(CallbackDataMetadata.TryCreate(typeof(DeleteCallbackPayload), out _));

        var serialized = serializer.Serialize(new DeleteCallbackPayload(42));
        var deserialized = serializer.Deserialize<DeleteCallbackPayload>(serialized);

        Assert.Equal("""{"id":42}""", serialized);
        Assert.Equal(42, deserialized.Id);
    }

    [Fact]
    public void CallbackDataMetadata_TryCreate_PreservesInvalidPayloadDiagnostics()
    {
        var first = Assert.Throws<InvalidOperationException>(
            () => CallbackDataMetadata.TryCreate(typeof(InvalidCompactCallback), out _));
        var second = Assert.Throws<InvalidOperationException>(
            () => CallbackDataMetadata.TryCreate(typeof(InvalidCompactCallback), out _));

        Assert.Equal(first.Message, second.Message);
        Assert.Contains("field 'Id' must not be nullable", first.Message);
    }

    [Fact]
    public async Task CustomCallbackSerializer_CanReplaceDefaultSerializer()
    {
        var customSerializer = new CustomCallbackDataSerializer();
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddSingleton<ICallbackDataSerializer>(customSerializer);
                services.AddTelegramHandler<TypedCallbackHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();
        using var scope = serviceProvider.CreateScope();
        var context = new UpdateContext(
            scope.ServiceProvider,
            new TelegramUpdatePayload(CreateCallbackUpdate("custom:7")));

        Assert.Same(customSerializer, context.GetTelegramContext().CallbackData);

        await scope.ServiceProvider.GetRequiredService<TeleFlow.Framework.Dispatching.IUpdateDispatcher>()
            .DispatchAsync(context);

        Assert.Equal(["typed-callback:7"], probe.Events);
    }

    [Fact]
    public async Task Dispatcher_DoesNotReadStateStoreForStatelessHandlers()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddSingleton<IStateStore, ThrowingStateStore>();
                services.AddMemoryStateStorage();
                services.AddTelegramHandler<AnyMessageHandler>();
            });

        var probe = serviceProvider.GetRequiredService<HandlerProbe>();

        await DispatchThroughMiddlewareAsync(serviceProvider, CreateMessageUpdate("stateless"));

        Assert.Equal(["message:stateless"], probe.Events);
    }

    [Fact]
    public async Task TypedStateAttribute_DispatchesUsingStateGroupProperty()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddMemoryStateStorage();
                services.AddTelegramHandler<TypedStateHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });
        var probe = serviceProvider.GetRequiredService<HandlerProbe>();
        var stateStore = serviceProvider.GetRequiredService<IStateStore>();

        await stateStore.SetStateAsync(
            StateKey.Create("telegram", "user:5", "chat:100"),
            RegistrationStates.Name.Id);

        await DispatchThroughMiddlewareAsync(serviceProvider, CreateMessageUpdate("state-value"));

        Assert.Equal(["typed-state:state-value"], probe.Events);
    }

    [Fact]
    public async Task MultiStateHandler_DispatchesFromEachDeclaredState()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddMemoryStateStorage();
                services.AddTelegramHandler<MultiStateMessageHandler>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });
        var probe = serviceProvider.GetRequiredService<HandlerProbe>();
        var stateKey = StateKey.Create("telegram", "user:5", "chat:100");
        var stateStore = serviceProvider.GetRequiredService<IStateStore>();

        await stateStore.SetStateAsync(stateKey, MultiState.First.Id);
        await DispatchThroughMiddlewareAsync(serviceProvider, CreateMessageUpdate("one"));

        await stateStore.SetStateAsync(stateKey, MultiState.Second.Id);
        await DispatchThroughMiddlewareAsync(serviceProvider, CreateMessageUpdate("two"));

        await stateStore.SetStateAsync(stateKey, "other");
        await DispatchThroughMiddlewareAsync(serviceProvider, CreateMessageUpdate("fallback"));

        Assert.Equal(["multi-state:one", "multi-state:two", "message:fallback"], probe.Events);
    }

    [Fact]
    public async Task SceneStep_DispatchesBySceneStateAndUsesStateData()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddMemoryStateStorage();
                services.AddTelegramHandler<RegistrationScene>();
                services.AddTelegramHandler<AnyMessageHandler>();
            });
        var probe = serviceProvider.GetRequiredService<HandlerProbe>();
        var stateKey = StateKey.Create("telegram", "user:5", "chat:100");
        var stateStore = serviceProvider.GetRequiredService<IStateStore>();
        var stateDataStore = serviceProvider.GetRequiredService<IStateDataStore>();

        await stateStore.SetStateAsync(stateKey, RegistrationScene.Name.Id);
        await DispatchThroughMiddlewareAsync(serviceProvider, CreateMessageUpdate("Ada"));

        Assert.Equal(["scene-name:Ada"], probe.Events);
        Assert.Equal(RegistrationScene.Age.Id, await stateStore.GetStateAsync(stateKey));
        Assert.Equal("\"Ada\"", await stateDataStore.GetDataAsync(stateKey, "name"));

        await DispatchThroughMiddlewareAsync(serviceProvider, CreateMessageUpdate("42"));

        Assert.Equal(["scene-name:Ada", "scene-age:Ada:42"], probe.Events);
        Assert.Null(await stateStore.GetStateAsync(stateKey));
        Assert.Null(await stateDataStore.GetDataAsync(stateKey, "name"));

        await DispatchThroughMiddlewareAsync(serviceProvider, CreateMessageUpdate("after"));

        Assert.Equal(["scene-name:Ada", "scene-age:Ada:42", "message:after"], probe.Events);
    }

    [Fact]
    public async Task SceneStep_DoesNotAutoAdvanceWithoutExplicitStateChange()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddMemoryStateStorage();
                services.AddTelegramHandler<ManualScene>();
            });
        var probe = serviceProvider.GetRequiredService<HandlerProbe>();
        var stateKey = StateKey.Create("telegram", "user:5", "chat:100");
        var stateStore = serviceProvider.GetRequiredService<IStateStore>();

        await stateStore.SetStateAsync(stateKey, ManualScene.First.Id);
        await DispatchThroughMiddlewareAsync(serviceProvider, CreateMessageUpdate("one"));
        await DispatchThroughMiddlewareAsync(serviceProvider, CreateMessageUpdate("two"));

        Assert.Equal(["manual-first:one", "manual-first:two"], probe.Events);
        Assert.Equal(ManualScene.First.Id, await stateStore.GetStateAsync(stateKey));
    }

    [Fact]
    public void SceneStep_RecordsSceneName()
    {
        var services = CreateBaseServices();

        services.AddTelegramHandler<RegistrationScene>();

        Assert.Equal(["registration", "registration"], GetRegisteredSceneNames(services));
    }

    [Fact]
    public void SceneStep_FailsClearlyWhenRouteIsMissing()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<SceneStepWithoutRouteScene>());

        Assert.Contains("requires an explicit Telegram route attribute", exception.Message);
    }

    [Fact]
    public void SceneStep_FailsClearlyWhenStatePropertyIsMissing()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<SceneStepMissingStateScene>());

        Assert.Contains("references missing scene state", exception.Message);
    }

    [Fact]
    public void SceneStep_FailsClearlyWhenMixedWithStateAttribute()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<SceneStepMixedStateScene>());

        Assert.Contains("cannot be mixed with StateAttribute", exception.Message);
    }

    [Fact]
    public void SceneStep_FailsClearlyWhenStateIdIsNotCanonical()
    {
        var services = CreateBaseServices();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandler<SceneStepNonCanonicalStateScene>());

        Assert.Contains("must return canonical state id 'registration:name'", exception.Message);
    }

    [Fact]
    public async Task SceneStep_UsesStateValueOverrideForCanonicalStateId()
    {
        using var serviceProvider = CreateServiceProvider(
            services =>
            {
                services.AddMemoryStateStorage();
                services.AddTelegramHandler<SceneStepStateValueScene>();
            });
        var probe = serviceProvider.GetRequiredService<HandlerProbe>();
        var stateStore = serviceProvider.GetRequiredService<IStateStore>();

        await stateStore.SetStateAsync(
            StateKey.Create("telegram", "user:5", "chat:100"),
            SceneStepStateValueScene.Name.Id);

        await DispatchThroughMiddlewareAsync(serviceProvider, CreateMessageUpdate("Ada"));

        Assert.Equal(["state-value-scene:Ada"], probe.Events);
    }

    private static ServiceCollection CreateBaseServices(
        Action<TelegramBotOptions>? configureBot = null)
    {
        var services = new ServiceCollection();
        services.AddTelegramBot(options =>
        {
            options.Token = "test-token";
            configureBot?.Invoke(options);
        });
        services.AddSingleton<HandlerProbe>();
        return services;
    }

    private static ServiceProvider CreateServiceProvider(
        Action<IServiceCollection> configureServices,
        Action<TelegramBotOptions>? configureBot = null)
    {
        var services = CreateBaseServices(configureBot);
        configureServices(services);
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

    private static async Task DispatchThroughMiddlewareAsync(
        ServiceProvider serviceProvider,
        Update update,
        CancellationToken cancellationToken = default)
    {
        var processor = new DefaultUpdateProcessor(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            serviceProvider.GetRequiredService<TeleFlow.Framework.Dispatching.IUpdateDispatcher>(),
            serviceProvider.GetServices<UpdateMiddlewareRegistration>());

        await processor.ProcessAsync(new TelegramUpdatePayload(update), cancellationToken);
    }

    private static IReadOnlyList<string?> GetRegisteredModuleNames(IServiceCollection services)
    {
        return services
            .Select(static descriptor => descriptor.ImplementationInstance)
            .Where(static instance =>
                instance?.GetType().FullName == "TeleFlow.Telegram.Internal.Handlers.TelegramHandlerDescriptor")
            .Select(static instance =>
                (string?)instance!.GetType().GetProperty("ModuleName")!.GetValue(instance))
            .ToArray();
    }

    private static IReadOnlyList<string?> GetRegisteredSceneNames(IServiceCollection services)
    {
        return services
            .Select(static descriptor => descriptor.ImplementationInstance)
            .Where(static instance =>
                instance?.GetType().FullName == "TeleFlow.Telegram.Internal.Handlers.TelegramHandlerDescriptor")
            .Select(static instance =>
                (string?)instance!.GetType().GetProperty("SceneName")!.GetValue(instance))
            .ToArray();
    }

    private static Update CreateMessageUpdate(
        string? text,
        Func<Message, Message>? configure = null,
        string chatType = "private",
        long fromUserId = 5)
    {
        var message = new Message
        {
            MessageId = 10,
            Date = 0,
            From = new User { Id = fromUserId, IsBot = false, FirstName = "User" },
            Chat = new Chat { Id = 100, Type = chatType },
            Text = text
        };

        message = configure?.Invoke(message) ?? message;

        return new Update
        {
            UpdateId = 1,
            Message = message
        };
    }

    private static PhotoSize CreatePhotoSize()
    {
        return new PhotoSize
        {
            FileId = "photo",
            FileUniqueId = "photo-unique",
            Width = 1,
            Height = 1
        };
    }

    private static Document CreateDocument()
    {
        return new Document
        {
            FileId = "document",
            FileUniqueId = "document-unique"
        };
    }

    private static Video CreateVideo()
    {
        return new Video
        {
            FileId = "video",
            FileUniqueId = "video-unique",
            Width = 1,
            Height = 1,
            Duration = 1
        };
    }

    private static Animation CreateAnimation()
    {
        return new Animation
        {
            FileId = "animation",
            FileUniqueId = "animation-unique",
            Width = 1,
            Height = 1,
            Duration = 1
        };
    }

    private static Audio CreateAudio()
    {
        return new Audio
        {
            FileId = "audio",
            FileUniqueId = "audio-unique",
            Duration = 1
        };
    }

    private static Voice CreateVoice()
    {
        return new Voice
        {
            FileId = "voice",
            FileUniqueId = "voice-unique",
            Duration = 1
        };
    }

    private static VideoNote CreateVideoNote()
    {
        return new VideoNote
        {
            FileId = "video-note",
            FileUniqueId = "video-note-unique",
            Length = 1,
            Duration = 1
        };
    }

    private static Sticker CreateSticker()
    {
        return new Sticker
        {
            FileId = "sticker",
            FileUniqueId = "sticker-unique",
            Type = "regular",
            Width = 1,
            Height = 1,
            IsAnimated = false,
            IsVideo = false
        };
    }

    private static Contact CreateContact()
    {
        return new Contact
        {
            PhoneNumber = "+10000000000",
            FirstName = "Contact"
        };
    }

    private static Venue CreateVenue()
    {
        return new Venue
        {
            Location = new Location { Latitude = 1, Longitude = 2 },
            Title = "Venue",
            Address = "Address"
        };
    }

    private static Poll CreatePoll()
    {
        return new Poll
        {
            Id = "poll",
            Question = "Question?",
            Options =
            [
                new PollOption
                {
                    PersistentId = "option",
                    Text = "Option",
                    VoterCount = 0
                }
            ],
            TotalVoterCount = 0,
            IsClosed = false,
            IsAnonymous = true,
            Type = "regular",
            AllowsMultipleAnswers = false,
            AllowsRevoting = true,
            MembersOnly = false
        };
    }

    private static Dice CreateDice()
    {
        return new Dice
        {
            Emoji = "dice",
            Value = 6
        };
    }

    private static TelegramTransportResponse CreateGetMeResponse()
    {
        return new TelegramTransportResponse(
            200,
            """{"ok":true,"result":{"id":42,"is_bot":true,"first_name":"TeleFlow Bot"}}""");
    }

    private static TelegramTransportResponse CreateOkResponse()
    {
        return new TelegramTransportResponse(
            200,
            """{"ok":true,"result":true}""");
    }

    private static Message CreateReplyMessage(bool isBot)
    {
        return new Message
        {
            MessageId = 9,
            Date = 0,
            Chat = new Chat { Id = 100, Type = "private" },
            From = new User { Id = isBot ? 1000 : 5, IsBot = isBot, FirstName = isBot ? "Bot" : "User" }
        };
    }

    private static Update CreateCallbackUpdate(
        string? data,
        bool includeMessage = false,
        Func<Message, Message>? configureMessage = null,
        long? inaccessibleChatId = null,
        Func<InaccessibleMessage, InaccessibleMessage>? configureInaccessibleMessage = null)
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

        var inaccessibleMessage = new InaccessibleMessage
        {
            Chat = new Chat { Id = inaccessibleChatId ?? 100, Type = "private" },
            MessageId = 10
        };

        inaccessibleMessage = configureInaccessibleMessage?.Invoke(inaccessibleMessage) ?? inaccessibleMessage;

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
                    : inaccessibleChatId is not null || configureInaccessibleMessage is not null
                        ? MaybeInaccessibleMessage.From(inaccessibleMessage)
                    : null
            }
        };
    }

    private static Update CreateChatMemberUpdate(
        bool isOwnUpdate,
        ChatMember oldChatMember,
        ChatMember newChatMember)
    {
        var chatMemberUpdated = new ChatMemberUpdated
        {
            Chat = new Chat { Id = 100, Type = "supergroup" },
            From = new User { Id = 42, IsBot = false, FirstName = "Actor" },
            Date = 0,
            OldChatMember = oldChatMember,
            NewChatMember = newChatMember
        };

        return new Update
        {
            UpdateId = 1,
            ChatMember = isOwnUpdate ? null : chatMemberUpdated,
            MyChatMember = isOwnUpdate ? chatMemberUpdated : null
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

    private static ChatMember BannedChatMember()
    {
        return ChatMember.From(new ChatMemberBanned
        {
            User = MemberUser(),
            UntilDate = 0
        });
    }

    private static ChatMember RestrictedChatMember(bool isMember)
    {
        return ChatMember.From(new ChatMemberRestricted
        {
            User = MemberUser(),
            IsMember = isMember,
            CanSendMessages = false,
            CanSendAudios = false,
            CanSendDocuments = false,
            CanSendPhotos = false,
            CanSendVideos = false,
            CanSendVideoNotes = false,
            CanSendVoiceNotes = false,
            CanSendPolls = false,
            CanSendOtherMessages = false,
            CanAddWebPagePreviews = false,
            CanReactToMessages = false,
            CanEditTag = false,
            CanChangeInfo = false,
            CanInviteUsers = false,
            CanPinMessages = false,
            CanManageTopics = false,
            UntilDate = 0
        });
    }

    private static ChatMember AdministratorChatMember()
    {
        return ChatMember.From(new ChatMemberAdministrator
        {
            User = MemberUser(),
            CanBeEdited = false,
            IsAnonymous = false,
            CanManageChat = true,
            CanDeleteMessages = true,
            CanManageVideoChats = true,
            CanRestrictMembers = true,
            CanPromoteMembers = true,
            CanChangeInfo = true,
            CanInviteUsers = true,
            CanPostStories = true,
            CanEditStories = true,
            CanDeleteStories = true
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

    public sealed class StartCommandHandler
    {
        [Command("start")]
        public Task Handle(
            MessageContext context,
            HandlerProbe probe,
            CancellationToken cancellationToken)
        {
            probe.Events.Add($"command:{context.TelegramMessage.Text}:{cancellationToken.CanBeCanceled}");
            return Task.CompletedTask;
        }
    }

    [Command("classstart")]
    public sealed class ClassBasedStartHandler : MessageHandler
    {
        public Task HandleAsync(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"class-start:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    [TextTemplate("class order {id:long}")]
    public sealed class ClassBasedOrderHandler : MessageHandler
    {
        public Task HandleAsync(MessageContext context, long id, HandlerProbe probe)
        {
            probe.Events.Add($"class-order:{id}");
            return Task.CompletedTask;
        }
    }

    [Callback<ClassBasedDeleteCallback>]
    public sealed class ClassBasedTypedCallbackHandler : CallbackHandler<ClassBasedDeleteCallback>
    {
        public Task HandleAsync(
            CallbackQueryContext context,
            ClassBasedDeleteCallback data,
            HandlerProbe probe)
        {
            probe.Events.Add($"class-callback:{data.Id}");
            return Task.CompletedTask;
        }
    }

    [ChatMemberUpdated]
    [ChatMemberTransition(TelegramMemberTransition.Join)]
    public sealed class ClassBasedJoinChatMemberHandler : ChatMemberUpdateHandler
    {
        public Task HandleAsync(ChatMemberUpdatedContext context, HandlerProbe probe)
        {
            probe.Events.Add($"class-chat-member:{context.Member.Id}");
            return Task.CompletedTask;
        }
    }

    [Command("missing-handle")]
    public sealed class ClassBasedMissingHandleHandler : MessageHandler
    {
    }

    [Command("class-route-without-base")]
    public sealed class ClassLevelRouteWithoutBaseHandler
    {
        public Task HandleAsync(MessageContext context)
        {
            return Task.CompletedTask;
        }
    }

    public sealed record ClassBasedDeleteCallback(long Id);

    public sealed class DuplicateStartCommandHandler
    {
        [Command("start")]
        public Task Handle(MessageContext context)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class DirectOnlyCommandHandler
    {
        [Command("direct-only")]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"direct-only:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class DirectSiblingCommandHandler
    {
        [Command("direct-sibling")]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"direct-sibling:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    [TelegramModule("admin")]
    public sealed class AdminModuleHandler
    {
        [Command("admin")]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"module:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class NonModuleCommandHandler
    {
        [Command("plain")]
        public Task Handle(MessageContext context)
        {
            return Task.CompletedTask;
        }
    }

    [TelegramModule("duplicates")]
    public sealed class DuplicateStartModuleHandler
    {
        [Command("start")]
        public Task Handle(MessageContext context)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class ExactTextMessageHandler
    {
        [Message]
        [Text("hello")]
        public ValueTask Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"text:{context.TelegramMessage.Text}");
            return ValueTask.CompletedTask;
        }
    }

    public sealed class LiteralTextRouteHandler
    {
        [Text("order {id}")]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"literal-text:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class CommandAndTextRouteHandler
    {
        [Command("help")]
        [Text("help")]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"command-or-text:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class CustomPrefixCommandHandler
    {
        [Command("start", Prefixes = new[] { "!" }, AllowSpaceAfterPrefix = true)]
        [Command("start", Prefixes = new[] { "." })]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"custom-prefix:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class BotMentionCommandHandler
    {
        [Command("start")]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"bot-mention:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class ShortPrefixFirstCommandHandler
    {
        [Command("short-first", Prefixes = new[] { "!", "!!" })]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("short-prefix-first");
            return Task.CompletedTask;
        }
    }

    public sealed class LongPrefixFirstCommandHandler
    {
        [Command("long-first", Prefixes = new[] { "!!", "!" })]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("long-prefix-first");
            return Task.CompletedTask;
        }
    }

    public sealed class OptionalPrefixCommandHandler
    {
        [Command("help", PrefixMode = CommandPrefixMode.Optional)]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"optional-prefix-command:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class ShortOptionalPrefixCommandHandler
    {
        [Command("я", PrefixMode = CommandPrefixMode.Optional)]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"short-optional-prefix-command:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class ShortNoPrefixCommandHandler
    {
        [Command("я", PrefixMode = CommandPrefixMode.NoPrefix)]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"short-no-prefix-command:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class TextTemplateRouteHandler
    {
        [TextTemplate("order {orderId:long}")]
        public Task Handle(MessageContext context, long orderId, HandlerProbe probe)
        {
            probe.Events.Add($"text-template:{orderId}");
            return Task.CompletedTask;
        }
    }

    public sealed class OptionalTextTemplateRouteHandler
    {
        [TextTemplate("order {orderId:long?}")]
        public Task Handle(MessageContext context, long? orderId, HandlerProbe probe)
        {
            probe.Events.Add($"optional-text-template:{orderId?.ToString() ?? "null"}");
            return Task.CompletedTask;
        }
    }

    public sealed class OptionalLiteralTextTemplateRouteHandler
    {
        [TextTemplate("hello {name?}")]
        public Task Handle(MessageContext context, string? name, HandlerProbe probe)
        {
            probe.Events.Add($"optional-literal:{name ?? "null"}");
            return Task.CompletedTask;
        }
    }

    public sealed class CommandTemplateRouteHandler
    {
        [CommandTemplate("ban {userId:int}")]
        public Task Handle(MessageContext context, int userId, HandlerProbe probe)
        {
            probe.Events.Add($"command-template:{userId}");
            return Task.CompletedTask;
        }
    }

    public sealed class OptionalPrefixCommandTemplateRouteHandler
    {
        [CommandTemplate("ban {userId:int}", PrefixMode = CommandPrefixMode.Optional)]
        public Task Handle(MessageContext context, int userId, HandlerProbe probe)
        {
            probe.Events.Add($"optional-prefix-command-template:{userId}");
            return Task.CompletedTask;
        }
    }

    public sealed class ShortOptionalPrefixCommandTemplateHandler
    {
        [CommandTemplate("я", PrefixMode = CommandPrefixMode.Optional)]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"short-optional-prefix-command-template:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class NoPrefixCommandTemplateRouteHandler
    {
        [CommandTemplate("ban {userId:int}", PrefixMode = CommandPrefixMode.NoPrefix)]
        public Task Handle(MessageContext context, int userId, HandlerProbe probe)
        {
            probe.Events.Add($"no-prefix-command-template:{userId}");
            return Task.CompletedTask;
        }
    }

    public sealed class ShortNoPrefixCommandTemplateHandler
    {
        [CommandTemplate("я", PrefixMode = CommandPrefixMode.NoPrefix)]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"short-no-prefix-command-template:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class OptionalCustomPrefixCommandTemplateRouteHandler
    {
        [CommandTemplate("ban {userId:int}", PrefixMode = CommandPrefixMode.Optional, Prefixes = new[] { "!" })]
        public Task Handle(MessageContext context, int userId, HandlerProbe probe)
        {
            probe.Events.Add($"optional-custom-prefix-command-template:{userId}");
            return Task.CompletedTask;
        }
    }

    public sealed record DispatcherDeepLinkPayload(long UserId);

    public sealed class DeepLinkStartCommandHandler
    {
        [CommandTemplate("start {payload}")]
        public Task Handle(MessageContext context, string payload, HandlerProbe probe)
        {
            var invite = context.Bot.DeepLinks.Deserialize<DispatcherDeepLinkPayload>(payload);
            probe.Events.Add($"deep-link:{invite.UserId}");
            return Task.CompletedTask;
        }
    }

    public sealed class OptionalCommandTemplateRouteHandler
    {
        [CommandTemplate("ban {userId:int?}")]
        public Task Handle(MessageContext context, int? userId, HandlerProbe probe)
        {
            probe.Events.Add($"optional-command-template:{userId?.ToString() ?? "null"}");
            return Task.CompletedTask;
        }
    }

    public sealed class PrefixedCommandTemplateRouteHandler
    {
        [CommandTemplate("/ban {userId:int}")]
        public Task Handle(MessageContext context, int userId)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class InvalidCommandPrefixHandler
    {
        [Command("start", Prefixes = new[] { "" })]
        public Task Handle(MessageContext context)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class TextRegexRouteHandler
    {
        [TextRegex(@"^order (?<orderId>\d+)$")]
        public Task Handle(MessageContext context, long orderId, HandlerProbe probe)
        {
            probe.Events.Add($"text-regex:{orderId}");
            return Task.CompletedTask;
        }
    }

    public sealed class OptionalRegexRouteValueHandler
    {
        [TextRegex(@"^order(?: (?<orderId>\d+))?$")]
        public Task Handle(MessageContext context, long orderId, HandlerProbe probe)
        {
            probe.Events.Add($"optional-regex:{orderId}");
            return Task.CompletedTask;
        }
    }

    public sealed class NonParsableRegexRouteValueHandler
    {
        [TextRegex(@"^order (?<orderId>.+)$")]
        public Task Handle(MessageContext context, long orderId, HandlerProbe probe)
        {
            probe.Events.Add($"non-parsable-regex:{orderId}");
            return Task.CompletedTask;
        }
    }

    public sealed class CommandRegexRouteHandler
    {
        [CommandRegex(@"^kick (?<userId>\d+)$", Prefixes = new[] { "!" })]
        public Task Handle(MessageContext context, int userId, HandlerProbe probe)
        {
            probe.Events.Add($"command-regex:{userId}");
            return Task.CompletedTask;
        }
    }

    public sealed class OptionalPrefixCommandRegexRouteHandler
    {
        [CommandRegex(@"^kick (?<userId>\d+)$", PrefixMode = CommandPrefixMode.Optional)]
        public Task Handle(MessageContext context, int userId, HandlerProbe probe)
        {
            probe.Events.Add($"optional-prefix-command-regex:{userId}");
            return Task.CompletedTask;
        }
    }

    public sealed class InvalidTemplateRouteHandler
    {
        [TextTemplate("order {id:guid}")]
        public Task Handle(MessageContext context, string id)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class NonCanonicalOptionalTemplateRouteHandler
    {
        [TextTemplate("order {id?:long}")]
        public Task Handle(MessageContext context, long? id)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class MissingRouteValueParameterHandler
    {
        [TextTemplate("order {id:int}")]
        public Task Handle(MessageContext context)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class OptionalTemplateWithNonNullableParameterHandler
    {
        [TextTemplate("order {id:int?}")]
        public Task Handle(MessageContext context, int id)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class RequiredTemplateWithNullableParameterHandler
    {
        [TextTemplate("order {id:int}")]
        public Task Handle(MessageContext context, int? id)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class TextRegexPriorityHandler
    {
        [TextRegex(@"^order (?<orderId>\d+)$")]
        public Task Handle(MessageContext context, long orderId, HandlerProbe probe)
        {
            probe.Events.Add("priority:regex");
            return Task.CompletedTask;
        }
    }

    public sealed class TextTemplatePriorityHandler
    {
        [TextTemplate("order {orderId:long}")]
        public Task Handle(MessageContext context, long orderId, HandlerProbe probe)
        {
            probe.Events.Add("priority:template");
            return Task.CompletedTask;
        }
    }

    public sealed class TextExactPriorityHandler
    {
        [Text("order 42")]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("priority:exact");
            return Task.CompletedTask;
        }
    }

    public sealed class GenericTextTemplatePriorityHandler
    {
        [TextTemplate("order {value}")]
        public Task Handle(MessageContext context, string value, HandlerProbe probe)
        {
            probe.Events.Add($"priority:generic-template:{value}");
            return Task.CompletedTask;
        }
    }

    public sealed class ConstrainedTextTemplatePriorityHandler
    {
        [TextTemplate("order {value:long}")]
        public Task Handle(MessageContext context, long value, HandlerProbe probe)
        {
            probe.Events.Add($"priority:constrained-template:{value}");
            return Task.CompletedTask;
        }
    }

    public sealed class OptionalTextTemplatePriorityHandler
    {
        [TextTemplate("item {value:long?}")]
        public Task Handle(MessageContext context, long? value, HandlerProbe probe)
        {
            probe.Events.Add($"priority:optional-template:{value?.ToString() ?? "null"}");
            return Task.CompletedTask;
        }
    }

    public sealed class RequiredTextTemplatePriorityHandler
    {
        [TextTemplate("item {value:long}")]
        public Task Handle(MessageContext context, long value, HandlerProbe probe)
        {
            probe.Events.Add($"priority:required-template:{value}");
            return Task.CompletedTask;
        }
    }

    public sealed class GenericCommandTemplatePriorityHandler
    {
        [CommandTemplate("ban {value}")]
        public Task Handle(MessageContext context, string value, HandlerProbe probe)
        {
            probe.Events.Add($"priority:generic-command-template:{value}");
            return Task.CompletedTask;
        }
    }

    public sealed class ConstrainedCommandTemplatePriorityHandler
    {
        [CommandTemplate("ban {value:long}")]
        public Task Handle(MessageContext context, long value, HandlerProbe probe)
        {
            probe.Events.Add($"priority:constrained-command-template:{value}");
            return Task.CompletedTask;
        }
    }

    public sealed class PrivateChatHandler
    {
        [Message]
        [ChatType(TelegramChatType.Private)]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"private-chat:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class ChatIdHandler
    {
        [Message]
        [ChatId(100)]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"chat-id:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class ChatUsernameHandler
    {
        [Message]
        [ChatUsername("@group")]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"chat-username:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class CallbackChatIdHandler
    {
        [Callback]
        [ChatId(100)]
        public Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            probe.Events.Add($"callback-chat-id:{context.TelegramCallbackQuery.Data}");
            return Task.CompletedTask;
        }
    }

    public sealed class CallbackChatUsernameHandler
    {
        [Callback]
        [ChatUsername("@group")]
        public Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            probe.Events.Add($"callback-chat-username:{context.TelegramCallbackQuery.Data}");
            return Task.CompletedTask;
        }
    }

    public sealed class CallbackPrivateChatTypeHandler
    {
        [Callback]
        [ChatType(TelegramChatType.Private)]
        public Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            probe.Events.Add($"callback-private-chat:{context.TelegramCallbackQuery.Data}");
            return Task.CompletedTask;
        }
    }

    public sealed class ChatMemberChatIdHandler
    {
        [ChatMemberUpdated]
        [ChatId(100)]
        public Task Handle(ChatMemberUpdatedContext context, HandlerProbe probe)
        {
            probe.Events.Add($"chat-member-chat-id:{context.TelegramChat.Id}");
            return Task.CompletedTask;
        }
    }

    public sealed class TopicMessageHandler
    {
        [Message]
        [MessageThreadId(42)]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"topic-message:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class ThreadedCallbackHandler
    {
        [Callback]
        [MessageThreadId(42)]
        public Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            probe.Events.Add($"threaded-callback:{context.TelegramCallbackQuery.Data}");
            return Task.CompletedTask;
        }
    }

    public sealed class AnyThreadCallbackHandler
    {
        [Callback]
        [HasMessageThread]
        public Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            probe.Events.Add($"has-thread-callback:{context.TelegramCallbackQuery.Data}");
            return Task.CompletedTask;
        }
    }

    public sealed class AnyThreadMessageHandler
    {
        [Message]
        [HasMessageThread]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"has-thread:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class FromUserHandler
    {
        [Message]
        [FromUser(5)]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"from-user:{context.TelegramMessage.From?.Id}");
            return Task.CompletedTask;
        }
    }

    public sealed class PhotoHandler
    {
        [Message]
        [HasPhoto]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("photo");
            return Task.CompletedTask;
        }
    }

    public sealed class DocumentHandler
    {
        [Message]
        [HasDocument]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("document");
            return Task.CompletedTask;
        }
    }

    public sealed class TextOnlyHandler
    {
        [Message]
        [HasText]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"has-text:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class CaptionHandler
    {
        [Message]
        [HasCaption]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"caption:{context.TelegramMessage.Caption}");
            return Task.CompletedTask;
        }
    }

    public sealed class VideoHandler
    {
        [Message]
        [HasVideo]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("video");
            return Task.CompletedTask;
        }
    }

    public sealed class AnimationHandler
    {
        [Message]
        [HasAnimation]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("animation");
            return Task.CompletedTask;
        }
    }

    public sealed class AudioHandler
    {
        [Message]
        [HasAudio]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("audio");
            return Task.CompletedTask;
        }
    }

    public sealed class VoiceHandler
    {
        [Message]
        [HasVoice]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("voice");
            return Task.CompletedTask;
        }
    }

    public sealed class VideoNoteHandler
    {
        [Message]
        [HasVideoNote]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("video-note");
            return Task.CompletedTask;
        }
    }

    public sealed class StickerHandler
    {
        [Message]
        [HasSticker]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("sticker");
            return Task.CompletedTask;
        }
    }

    public sealed class ContactHandler
    {
        [Message]
        [HasContact]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("contact");
            return Task.CompletedTask;
        }
    }

    public sealed class LocationHandler
    {
        [Message]
        [HasLocation]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("location");
            return Task.CompletedTask;
        }
    }

    public sealed class VenueHandler
    {
        [Message]
        [HasVenue]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("venue");
            return Task.CompletedTask;
        }
    }

    public sealed class PollHandler
    {
        [Message]
        [HasPoll]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("poll");
            return Task.CompletedTask;
        }
    }

    public sealed class DiceHandler
    {
        [Message]
        [HasDice]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("dice");
            return Task.CompletedTask;
        }
    }

    public sealed class BotSenderHandler
    {
        [Message]
        [FromBot]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("from-bot");
            return Task.CompletedTask;
        }
    }

    public sealed class HumanSenderHandler
    {
        [Message]
        [FromBot(false)]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("from-human");
            return Task.CompletedTask;
        }
    }

    public sealed class PremiumSenderHandler
    {
        [Message]
        [FromPremiumUser]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("premium");
            return Task.CompletedTask;
        }
    }

    public sealed class ReplyHandler
    {
        [Message]
        [IsReply]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("is-reply");
            return Task.CompletedTask;
        }
    }

    public sealed class ReplyToBotHandler
    {
        [Message]
        [ReplyToBot]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("reply-to-bot");
            return Task.CompletedTask;
        }
    }

    public sealed class CustomFilterMessageHandler
    {
        [Message]
        [UseFilter<AllowMessageFilter>]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"custom-filter:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class DenyCustomFilterMessageHandler
    {
        [Message]
        [UseFilter<DenyMessageFilter>]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"deny-custom-filter:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class ParameterizedCustomFilterMessageHandler
    {
        [Message]
        [RequireMessageText("custom", IgnoreCase = true)]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"parameterized-filter:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    [UseFilter<AllowUpdateFilter>]
    public sealed class AndCustomFilterMessageHandler
    {
        [Message]
        [UseFilter<DenyMessageFilter>]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"and-custom-filter:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class ThrowingCustomFilterMessageHandler
    {
        [Message]
        [UseFilter<ThrowingMessageFilter>]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"throwing-custom-filter:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class CustomFilterCallbackHandler
    {
        [Callback]
        [UseFilter<AllowCallbackFilter>]
        public Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            probe.Events.Add($"custom-callback:{context.TelegramCallbackQuery.Data}");
            return Task.CompletedTask;
        }
    }

    public sealed class BuiltInFilterBeforeCustomFilterHandler
    {
        [Message]
        [ChatId(999)]
        [UseFilter<ThrowingMessageFilter>]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"built-in-before-custom:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class CallbackWithMessageCustomFilterHandler
    {
        [Callback]
        [UseFilter<AllowMessageFilter>]
        public Task Handle(CallbackQueryContext context)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class TypedCallbackWithThrowingFilterHandler
    {
        [Callback<DeleteCallbackPayload>]
        [UseFilter<ThrowingCallbackFilter>]
        public Task Handle(
            CallbackQueryContext context,
            DeleteCallbackPayload payload,
            HandlerProbe probe)
        {
            probe.Events.Add($"typed-callback-filter:{payload.Id}");
            return Task.CompletedTask;
        }
    }

    public sealed class TypedCallbackWithPrefixFilterHandler
    {
        [Callback<DeleteCallbackPayload>]
        [CallbackDataPrefix("del:")]
        public Task Handle(
            CallbackQueryContext context,
            DeleteCallbackPayload payload,
            HandlerProbe probe)
        {
            probe.Events.Add($"typed-prefix:{payload.Id}");
            return Task.CompletedTask;
        }
    }

    [ChatType(TelegramChatType.Private)]
    public sealed class PrivateTextHandler
    {
        [Message]
        [HasText]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"private-text:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    [ChatType(TelegramChatType.Private)]
    public abstract class PrivateHandlerBase
    {
    }

    public sealed class InheritedPrivateTextHandler : PrivateHandlerBase
    {
        [Message]
        [HasText]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"inherited-private-text:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public abstract class BaseMessageHandler
    {
        [Message]
        public virtual Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"base-inherited:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class InheritedMessageHandlerWithoutOverride : BaseMessageHandler
    {
    }

    public sealed class OwnAndInheritedMessageHandlerWithoutOverride : BaseMessageHandler
    {
        [Command("own")]
        public Task Own(MessageContext context)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class OverrideInheritedMessageHandler : BaseMessageHandler
    {
        public override Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"override-inherited:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class CallbackWithMessageFilterHandler
    {
        [Callback]
        [HasText]
        public Task Handle(CallbackQueryContext context)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingStateStore : IStateStore
    {
        public ValueTask<string?> GetStateAsync(StateKey key, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("State store should not be read for stateless handlers.");
        }

        public ValueTask SetStateAsync(StateKey key, string state, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("State store should not be written for stateless handlers.");
        }

        public ValueTask ClearStateAsync(StateKey key, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("State store should not be cleared for stateless handlers.");
        }
    }

    public sealed class AnyMessageHandler
    {
        [Message]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"message:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class RawCallbackHandler
    {
        [Callback]
        public Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            probe.Events.Add($"callback:{context.TelegramCallbackQuery.Data}");
            return Task.CompletedTask;
        }
    }

    public sealed class HasCallbackDataHandler
    {
        [Callback]
        [HasCallbackData]
        public Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            probe.Events.Add($"has-callback-data:{context.TelegramCallbackQuery.Data}");
            return Task.CompletedTask;
        }
    }

    public sealed class AdminCallbackPrefixHandler
    {
        [Callback]
        [CallbackDataPrefix("admin:")]
        public Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            probe.Events.Add($"admin-callback:{context.TelegramCallbackQuery.Data}");
            return Task.CompletedTask;
        }
    }

    public sealed class TrimmedCallbackPrefixHandler
    {
        [Callback]
        [CallbackDataPrefix(" admin: ")]
        public Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            probe.Events.Add($"trimmed-callback:{context.TelegramCallbackQuery.Data}");
            return Task.CompletedTask;
        }
    }

    public sealed class LateServiceCallbackHandler
    {
        [Callback]
        public Task Handle(CallbackQueryContext context, LateCallbackService service)
        {
            service.Events.Add($"late-service:{context.TelegramCallbackQuery.Data}");
            return Task.CompletedTask;
        }
    }

    public sealed class RawCallbackWithPayloadHandler
    {
        [Callback]
        public Task Handle(CallbackQueryContext context, CompactDeleteCallback payload)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class TypedCallbackHandler
    {
        [Callback<DeleteCallbackPayload>]
        public Task Handle(
            CallbackQueryContext context,
            DeleteCallbackPayload payload,
            HandlerProbe probe)
        {
            probe.Events.Add($"typed-callback:{payload.Id}");
            return Task.CompletedTask;
        }
    }

    public sealed class CompactCallbackHandler
    {
        [Callback<CompactDeleteCallback>]
        public Task Handle(
            CallbackQueryContext context,
            CompactDeleteCallback payload,
            HandlerProbe probe)
        {
            probe.Events.Add($"compact-callback:{payload.Name}:{payload.Id}");
            return Task.CompletedTask;
        }
    }

    public sealed class DuplicateCompactCallbackHandler
    {
        [Callback<DuplicateCompactDeleteCallback>]
        public Task Handle(
            CallbackQueryContext context,
            DuplicateCompactDeleteCallback payload)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class TypedStateHandler
    {
        [Message]
        [State<RegistrationStates>(nameof(RegistrationStates.Name))]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"typed-state:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    [StateGroup("multi")]
    public sealed class MultiState
    {
        public static State First => State.Create("multi:first");

        public static State Second => State.Create("multi:second");
    }

    public sealed class MultiStateMessageHandler
    {
        [Message]
        [State<MultiState>(nameof(MultiState.First))]
        [State<MultiState>(nameof(MultiState.Second))]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"multi-state:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    [Scene("registration")]
    public sealed class RegistrationScene
    {
        public static State Name => State.Create("registration:name");

        public static State Age => State.Create("registration:age");

        [Message]
        [SceneStep(nameof(Name))]
        public async Task NameStep(MessageContext context, HandlerProbe probe)
        {
            var name = context.TelegramMessage.Text ?? string.Empty;

            await context.State.Data.SetAsync("name", name, context.CancellationToken);
            await context.State.SetAsync(Age, context.CancellationToken);
            probe.Events.Add($"scene-name:{name}");
        }

        [Message]
        [SceneStep(nameof(Age))]
        public async Task AgeStep(MessageContext context, HandlerProbe probe)
        {
            var name = await context.State.Data.GetRequiredAsync<string>("name", context.CancellationToken);

            await context.State.ResetAsync(context.CancellationToken);
            probe.Events.Add($"scene-age:{name}:{context.TelegramMessage.Text}");
        }
    }

    [Scene("manual")]
    public sealed class ManualScene
    {
        public static State First => State.Create("manual:first");

        public static State Second => State.Create("manual:second");

        [Message]
        [SceneStep(nameof(First))]
        public Task FirstStep(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"manual-first:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }

        [Message]
        [SceneStep(nameof(Second))]
        public Task SecondStep(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"manual-second:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    [Scene("missing-route")]
    public sealed class SceneStepWithoutRouteScene
    {
        public static State Name => State.Create("missing-route:name");

        [SceneStep(nameof(Name))]
        public Task Handle(MessageContext context)
        {
            return Task.CompletedTask;
        }
    }

    [Scene("missing-state")]
    public sealed class SceneStepMissingStateScene
    {
        public static State Name => State.Create("missing-state:name");

        [Message]
        [SceneStep("Unknown")]
        public Task Handle(MessageContext context)
        {
            return Task.CompletedTask;
        }
    }

    [Scene("mixed-state")]
    public sealed class SceneStepMixedStateScene
    {
        public static State Name => State.Create("mixed-state:name");

        [Message]
        [SceneStep(nameof(Name))]
        [State("legacy")]
        public Task Handle(MessageContext context)
        {
            return Task.CompletedTask;
        }
    }

    [Scene("registration")]
    public sealed class SceneStepNonCanonicalStateScene
    {
        public static State Name => State.Create("registration:fullName");

        [Message]
        [SceneStep(nameof(Name))]
        public Task Handle(MessageContext context)
        {
            return Task.CompletedTask;
        }
    }

    [Scene("registration")]
    public sealed class SceneStepStateValueScene
    {
        [StateValue("fullName")]
        public static State Name => State.Create("registration:fullName");

        [Message]
        [SceneStep(nameof(Name))]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"state-value-scene:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class ThrowingMessageHandler
    {
        public static Exception? Exception { get; set; }

        [Message]
        public Task Handle(MessageContext context)
        {
            return Task.FromException(Exception ?? new InvalidOperationException("No exception configured."));
        }
    }

    public sealed class HandledErrorHandler
    {
        [Error<InvalidOperationException>]
        public Task<TelegramErrorHandlingResult> Handle(InvalidOperationException exception, HandlerProbe probe)
        {
            probe.Events.Add($"handled:{exception.Message}");
            return Task.FromResult(TelegramErrorHandlingResult.Handled);
        }
    }

    public sealed class UnhandledErrorHandler
    {
        [Error<InvalidOperationException>]
        public ValueTask<TelegramErrorHandlingResult> Handle(InvalidOperationException exception, HandlerProbe probe)
        {
            probe.Events.Add($"unhandled:{exception.Message}");
            return ValueTask.FromResult(TelegramErrorHandlingResult.Unhandled);
        }
    }

    public sealed class ModuleFailureException : InvalidOperationException
    {
        public ModuleFailureException()
            : base("module failed")
        {
        }
    }

    [TelegramModule("module")]
    public sealed class ModuleThrowingHandler
    {
        [Command("module-boom")]
        public Task Handle(MessageContext context)
        {
            return Task.FromException(new ModuleFailureException());
        }
    }

    [TelegramModule("module")]
    public sealed class ModuleScopedErrorHandler
    {
        [Error]
        public TelegramErrorHandlingResult Handle(Exception exception, HandlerProbe probe)
        {
            probe.Events.Add($"module-error:{exception.Message}");
            return TelegramErrorHandlingResult.Handled;
        }
    }

    public sealed class GlobalModuleErrorHandler
    {
        [Error<ModuleFailureException>]
        public TelegramErrorHandlingResult Handle(ModuleFailureException exception, HandlerProbe probe)
        {
            probe.Events.Add($"global-error:{exception.Message}");
            return TelegramErrorHandlingResult.Handled;
        }
    }

    public sealed class SpecificErrorHandlerFailureException : InvalidOperationException
    {
        public SpecificErrorHandlerFailureException()
            : base("specific failure")
        {
        }
    }

    public sealed class BroadInvalidOperationErrorHandler
    {
        [Error<InvalidOperationException>]
        public TelegramErrorHandlingResult Handle(InvalidOperationException exception, HandlerProbe probe)
        {
            probe.Events.Add($"broad-error:{exception.Message}");
            return TelegramErrorHandlingResult.Handled;
        }
    }

    public sealed class SpecificFailureErrorHandler
    {
        [Error<SpecificErrorHandlerFailureException>]
        public TelegramErrorHandlingResult Handle(SpecificErrorHandlerFailureException exception, HandlerProbe probe)
        {
            probe.Events.Add($"specific-error:{exception.Message}");
            return TelegramErrorHandlingResult.Handled;
        }
    }

    public sealed class FirstTieErrorHandler
    {
        [Error<InvalidOperationException>]
        public TelegramErrorHandlingResult Handle(InvalidOperationException exception, HandlerProbe probe)
        {
            probe.Events.Add($"first-tie:{exception.Message}");
            return TelegramErrorHandlingResult.Unhandled;
        }
    }

    public sealed class SecondTieErrorHandler
    {
        [Error<InvalidOperationException>]
        public TelegramErrorHandlingResult Handle(InvalidOperationException exception, HandlerProbe probe)
        {
            probe.Events.Add($"second-tie:{exception.Message}");
            return TelegramErrorHandlingResult.Handled;
        }
    }

    public sealed class TypedUnhandledErrorHandler
    {
        [Error<InvalidOperationException>]
        public TelegramErrorHandlingResult Handle(InvalidOperationException exception, HandlerProbe probe)
        {
            probe.Events.Add($"typed-unhandled:{exception.Message}");
            return TelegramErrorHandlingResult.Unhandled;
        }
    }

    public sealed class CatchAllHandledErrorHandler
    {
        [Error]
        public TelegramErrorHandlingResult Handle(Exception exception, HandlerProbe probe)
        {
            probe.Events.Add($"catch-all:{exception.Message}");
            return TelegramErrorHandlingResult.Handled;
        }
    }

    public sealed class RouteValueFailureException : InvalidOperationException
    {
        public RouteValueFailureException()
            : base("route value failed")
        {
        }
    }

    public sealed class RouteValueThrowingHandler
    {
        [CommandTemplate("orders {orderId:long}")]
        public Task Handle(MessageContext context, long orderId)
        {
            return Task.FromException(new RouteValueFailureException());
        }
    }

    public sealed class RouteValueErrorHandler
    {
        [Error<RouteValueFailureException>]
        public TelegramErrorHandlingResult Handle(
            TelegramErrorContext error,
            MessageContext context,
            RouteValueFailureException exception,
            long orderId,
            HandlerProbe probe,
            CancellationToken cancellationToken)
        {
            probe.Events.Add(
                $"bound:{orderId}:{exception.GetType().Name}:{error.HandlerMethodName}:{error.HandlerType.Name}:{context.GetType().Name}:{cancellationToken.IsCancellationRequested}");
            return TelegramErrorHandlingResult.Handled;
        }
    }

    public sealed class IncompatibleRouteValueErrorHandler
    {
        [Error<RouteValueFailureException>]
        public TelegramErrorHandlingResult Handle(
            RouteValueFailureException exception,
            string orderId,
            HandlerProbe probe)
        {
            probe.Events.Add($"incompatible:{orderId}:{exception.Message}");
            return TelegramErrorHandlingResult.Handled;
        }
    }

    public sealed class InvalidErrorReturnHandler
    {
        [Error]
        public Task Handle(Exception exception)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class InvalidErrorResultHandler
    {
        [Error]
        public TelegramErrorHandlingResult Handle(Exception exception)
        {
            return (TelegramErrorHandlingResult)42;
        }
    }

    public sealed class CancellableThrowingHandler
    {
        [Message]
        public Task Handle(MessageContext context, CancellationToken cancellationToken)
        {
            return Task.FromCanceled(cancellationToken);
        }
    }

    public sealed class InvalidReturnHandler
    {
        [Message]
        public void Handle(MessageContext context)
        {
        }
    }

    public sealed class OneTelegramRequestMessageHandler
    {
        [Message]
        public async Task Handle(MessageContext context, HandlerProbe probe)
        {
            var user = await context.Bot.GetMeAsync();
            probe.Events.Add($"one-request:{user.Id}");
        }
    }

    public sealed class TwoTelegramRequestsCallbackHandler
    {
        [Callback]
        public async Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            await context.Bot.GetMeAsync();
            await context.Bot.GetMeAsync();
            probe.Events.Add("two-requests");
        }
    }

    public sealed class AutoAnswerCallbackHandler
    {
        [Callback]
        [AutoAnswerCallback("Deleted")]
        public Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            probe.Events.Add($"auto-answer:{context.TelegramCallbackQuery.Data}");
            return Task.CompletedTask;
        }
    }

    public sealed class ManualAnswerCallbackHandler
    {
        [Callback]
        public async Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            probe.Events.Add($"manual-answer:{context.TelegramCallbackQuery.Data}");
            await context.Callback.AnswerAsync("Manual");
        }
    }

    public sealed class DisabledAutoAnswerCallbackHandler
    {
        [Callback]
        [AutoAnswerCallback(Enabled = false)]
        public Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            probe.Events.Add($"disabled-auto-answer:{context.TelegramCallbackQuery.Data}");
            return Task.CompletedTask;
        }
    }

    public sealed class ThrowingAutoAnswerCallbackHandler
    {
        [Callback]
        public Task Handle(CallbackQueryContext context)
        {
            throw new InvalidOperationException("callback failed");
        }
    }

    public sealed class AutoAnswerFailureErrorHandler
    {
        [Error]
        public TelegramErrorHandlingResult Handle(Exception exception, HandlerProbe probe)
        {
            probe.Events.Add($"auto-answer-error:{exception.GetType().Name}");
            return TelegramErrorHandlingResult.Handled;
        }
    }

    public sealed class JoinChatMemberHandler
    {
        [ChatMemberUpdated]
        [ChatMemberTransition(TelegramMemberTransition.Join)]
        public Task Handle(ChatMemberUpdatedContext context, HandlerProbe probe)
        {
            probe.Events.Add($"join:{context.Member.Id}:{context.TelegramChat.Id}");
            return Task.CompletedTask;
        }
    }

    public sealed class BroadChatMemberHandler
    {
        [ChatMemberUpdated]
        public Task Handle(ChatMemberUpdatedContext context, HandlerProbe probe)
        {
            probe.Events.Add($"chat-member:{context.Member.Id}:{context.TelegramChat.Id}");
            return Task.CompletedTask;
        }
    }

    public sealed class AssemblyScanChatMemberHandler
    {
        [ChatMemberUpdated]
        public Task Handle(ChatMemberUpdatedContext context, HandlerProbe probe)
        {
            probe.Events.Add($"assembly-scan-chat-member:{context.Member.Id}");
            return Task.CompletedTask;
        }
    }

    public sealed class MyChatMemberChangedHandler
    {
        [MyChatMemberUpdated]
        [ChatMemberChanged(TelegramMemberStatusSet.IsNotMember, TelegramMemberStatusSet.IsMember)]
        public Task Handle(ChatMemberUpdatedContext context, HandlerProbe probe)
        {
            probe.Events.Add($"my-chat-member:{context.Member.Id}:{context.TelegramChat.Id}");
            return Task.CompletedTask;
        }
    }

    public sealed class PromotedChatMemberHandler
    {
        [ChatMemberUpdated]
        [ChatMemberTransition(TelegramMemberTransition.Promoted)]
        public Task Handle(ChatMemberUpdatedContext context, HandlerProbe probe)
        {
            probe.Events.Add($"promoted:{context.Member.Id}");
            return Task.CompletedTask;
        }
    }

    public sealed class DemotedChatMemberHandler
    {
        [ChatMemberUpdated]
        [ChatMemberTransition(TelegramMemberTransition.Demoted)]
        public Task Handle(ChatMemberUpdatedContext context, HandlerProbe probe)
        {
            probe.Events.Add($"demoted:{context.Member.Id}");
            return Task.CompletedTask;
        }
    }

    public sealed class AdminCommandRoleHandler
    {
        [Command("admin")]
        [RequireTelegramRole(TelegramMemberStatusSet.IsAdmin)]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"role-command:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class RoleFallbackMessageHandler
    {
        [Message]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"role-fallback:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    [RequireTelegramRole(TelegramMemberStatusSet.IsAdmin)]
    public sealed class AdminAndCreatorRoleHandler
    {
        [Message]
        [RequireTelegramRole(TelegramMemberStatusSet.Creator)]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add("role-admin-creator");
            return Task.CompletedTask;
        }
    }

    public sealed class AdminCallbackRoleHandler
    {
        [Callback]
        [RequireTelegramRole(TelegramMemberStatusSet.IsAdmin)]
        public Task Handle(CallbackQueryContext context, HandlerProbe probe)
        {
            probe.Events.Add($"role-callback:{context.TelegramCallbackQuery.Data}");
            return Task.CompletedTask;
        }
    }

    public sealed class AdminChatMemberRoleHandler
    {
        [ChatMemberUpdated]
        [RequireTelegramRole(TelegramMemberStatusSet.IsAdmin)]
        public Task Handle(ChatMemberUpdatedContext context, HandlerProbe probe)
        {
            probe.Events.Add($"role-chat-member:{context.Actor.Id}:{context.Member.Id}");
            return Task.CompletedTask;
        }
    }

    public sealed class DenyCustomFilterRoleHandler
    {
        [Message]
        [UseFilter<DenyMessageFilter>]
        [RequireTelegramRole(TelegramMemberStatusSet.IsAdmin)]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"deny-role-filter:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class AllowCustomFilterRoleHandler
    {
        [Message]
        [UseFilter<AllowMessageFilter>]
        [RequireTelegramRole(TelegramMemberStatusSet.IsAdmin)]
        public Task Handle(MessageContext context, HandlerProbe probe)
        {
            probe.Events.Add($"allow-role-filter:{context.TelegramMessage.Text}");
            return Task.CompletedTask;
        }
    }

    public sealed class HandlerProbe
    {
        public List<string> Events { get; } = [];
    }

    public sealed class AllowMessageFilter : ITelegramFilter<MessageContext>
    {
        public ValueTask<bool> MatchesAsync(
            MessageContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(true);
        }
    }

    public sealed class DenyMessageFilter : ITelegramFilter<MessageContext>
    {
        public ValueTask<bool> MatchesAsync(
            MessageContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(false);
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class RequireMessageTextAttribute : TelegramFilterAttribute<RequireMessageTextFilter>
    {
        public RequireMessageTextAttribute(string text)
        {
            Text = text;
        }

        public string Text { get; }

        public bool IgnoreCase { get; set; }
    }

    public sealed class RequireMessageTextFilter : ITelegramFilter<MessageContext, RequireMessageTextAttribute>
    {
        public ValueTask<bool> MatchesAsync(
            MessageContext context,
            RequireMessageTextAttribute attribute,
            CancellationToken cancellationToken = default)
        {
            var comparison = attribute.IgnoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            var matches = string.Equals(context.TelegramMessage.Text, attribute.Text, comparison);
            return ValueTask.FromResult(matches);
        }
    }

    public sealed class AllowUpdateFilter : ITelegramFilter<TelegramUpdateContext>
    {
        public ValueTask<bool> MatchesAsync(
            TelegramUpdateContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(true);
        }
    }

    public sealed class AllowCallbackFilter : ITelegramFilter<CallbackQueryContext>
    {
        public ValueTask<bool> MatchesAsync(
            CallbackQueryContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(true);
        }
    }

    public sealed class ThrowingMessageFilter : ITelegramFilter<MessageContext>
    {
        public ValueTask<bool> MatchesAsync(
            MessageContext context,
            CancellationToken cancellationToken = default)
        {
            throw new CustomFilterException();
        }
    }

    public sealed class ThrowingCallbackFilter : ITelegramFilter<CallbackQueryContext>
    {
        public ValueTask<bool> MatchesAsync(
            CallbackQueryContext context,
            CancellationToken cancellationToken = default)
        {
            throw new CustomFilterException();
        }
    }

    public sealed class CustomFilterException : Exception
    {
    }

    public sealed class LateCallbackService
    {
        public List<string> Events { get; } = [];
    }

    public sealed record DeleteCallbackPayload(long Id);

    [CallbackData("del")]
    public sealed record CompactDeleteCallback(string Name, int Id);

    [CallbackData("del")]
    public sealed record DuplicateCompactDeleteCallback(long Id);

    [CallbackData("bad")]
    public sealed record InvalidCompactCallback(int? Id);

    public sealed record LargeCallbackPayload(string Value);

    [StateGroup("registration")]
    public sealed class RegistrationStates
    {
        public static State Name => State.Create("registration:name");
    }

    private sealed class SinglePayloadUpdateSource(IUpdatePayload payload) : IUpdateSource
    {
        public Task StartAsync(
            Func<IUpdatePayload, CancellationToken, Task> updateHandler,
            CancellationToken cancellationToken = default)
        {
            return updateHandler(payload, cancellationToken);
        }
    }

    private sealed class CustomCallbackDataSerializer : ICallbackDataSerializer
    {
        public string Serialize<TPayload>(TPayload payload)
        {
            return payload is DeleteCallbackPayload deletePayload
                ? $"custom:{deletePayload.Id}"
                : throw new InvalidOperationException("Unexpected callback payload type.");
        }

        public TPayload Deserialize<TPayload>(string serializedPayload)
        {
            if (typeof(TPayload) == typeof(DeleteCallbackPayload) &&
                serializedPayload.StartsWith("custom:", StringComparison.Ordinal) &&
                long.TryParse(serializedPayload["custom:".Length..], out var id))
            {
                return (TPayload)(object)new DeleteCallbackPayload(id);
            }

            throw new InvalidOperationException("Unexpected callback data.");
        }
    }

    private sealed class RouteDeserializingCallbackDataSerializer(bool canDeserialize) : ICallbackDataRouteDeserializer
    {
        public int RouteDeserializeCalls { get; private set; }

        public int PayloadBindingCalls { get; private set; }

        public int PublicDeserializeCalls { get; private set; }

        public bool TryDeserializeForRoute(Type payloadType, string serializedPayload, out object? payload)
        {
            RouteDeserializeCalls++;

            if (!canDeserialize ||
                payloadType != typeof(CompactDeleteCallback) ||
                !string.Equals(serializedPayload, "del:item:9", StringComparison.Ordinal))
            {
                payload = null;
                return false;
            }

            PayloadBindingCalls++;
            payload = new CompactDeleteCallback("item", 9);
            return true;
        }

        public string Serialize<TPayload>(TPayload payload)
        {
            throw new InvalidOperationException("Route deserializer test double is dispatch-only.");
        }

        public TPayload Deserialize<TPayload>(string serializedPayload)
        {
            PublicDeserializeCalls++;
            throw new InvalidOperationException("Route deserializer should be used during handler selection.");
        }
    }

    private sealed class BrokenCallbackDataSerializer : ICallbackDataSerializer
    {
        public string Serialize<TPayload>(TPayload payload)
        {
            throw new InvalidOperationException("Broken callback serializer.");
        }

        public TPayload Deserialize<TPayload>(string serializedPayload)
        {
            throw new InvalidOperationException("Broken callback serializer.");
        }
    }

    private sealed class InvalidPayloadCallbackDataSerializer : ICallbackDataSerializer
    {
        public string Serialize<TPayload>(TPayload payload)
        {
            throw new JsonException("Invalid callback payload.");
        }

        public TPayload Deserialize<TPayload>(string serializedPayload)
        {
            throw new JsonException("Invalid callback payload.");
        }
    }

    private sealed class CancelingCallbackDataSerializer : ICallbackDataSerializer
    {
        public string Serialize<TPayload>(TPayload payload)
        {
            throw new OperationCanceledException();
        }

        public TPayload Deserialize<TPayload>(string serializedPayload)
        {
            throw new OperationCanceledException();
        }
    }

    private sealed class ModuleCustomDispatcher : TeleFlow.Framework.Dispatching.IUpdateDispatcher
    {
        public Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class SequencedTelegramTransport(params TelegramTransportResponse[] responses) : ITelegramTransport
    {
        private readonly Queue<TelegramTransportResponse> _responses = new(responses);

        public Task<TelegramTransportResponse> SendAsync(
            TelegramTransportRequest request,
            CancellationToken cancellationToken = default)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No queued Telegram transport responses remain.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class SequencedTimestampTimeProvider(params long[] timestamps) : TimeProvider
    {
        private int _index;

        public override long TimestampFrequency => 1000;

        public override long GetTimestamp()
        {
            if (_index >= timestamps.Length)
            {
                return timestamps[^1];
            }

            return timestamps[_index++];
        }
    }

    private sealed class RecordingTelegramTransport(params TelegramTransportResponse[] responses) : ITelegramTransport
    {
        private readonly Queue<TelegramTransportResponse> _responses = new(responses);

        public List<RecordedTelegramTransportRequest> Requests { get; } = [];

        public Task<TelegramTransportResponse> SendAsync(
            TelegramTransportRequest request,
            CancellationToken cancellationToken = default)
        {
            var json = request.Content is TelegramJsonTransportContent jsonContent
                ? jsonContent.Json
                : string.Empty;
            Requests.Add(new RecordedTelegramTransportRequest(request.MethodName, json));

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No queued Telegram transport responses remain.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed record RecordedTelegramTransportRequest(string MethodName, string Json);

    private sealed class RecordingTelegramRoleResolver(TelegramMemberStatusSet status) : ITelegramChatMemberStatusResolver
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

}
