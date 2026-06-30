using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Annotations;
using TeleFlow.Core.Dispatching;
using TeleFlow.Core.States;
using TeleFlow.Core.Updates;
using TeleFlow.Generators;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.ArchitectureTests;

#pragma warning disable TLF900 // These tests intentionally cover deprecated reflection assembly registration until removal.

public sealed class TelegramReflectionAssemblyRegistrationTests
{
    [Fact]
    public async Task AddTelegramHandlersFromAssemblyReflection_RegistersNormalHandlers()
    {
        var assembly = CompileAssembly(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.ArchitectureTests;
            using TeleFlow.Telegram;

            namespace ReflectionRuntime;

            public sealed class ReflectionNormalHandler
            {
                [Command("reflection-normal")]
                public Task Handle(MessageContext context, ReflectionRegistrationProbe probe)
                {
                    probe.Events.Add($"normal:{context.TelegramMessage.Text}");
                    return Task.CompletedTask;
                }
            }
            """);
        var probe = new ReflectionRegistrationProbe();
        var services = CreateBaseServices(probe);

        services.AddTelegramHandlersFromAssemblyReflection(assembly);

        await DispatchAsync(CreateServiceProvider(services), CreateMessageUpdate("/reflection-normal"));

        Assert.Equal(["normal:/reflection-normal"], probe.Events);
    }

    [Fact]
    public async Task AddTelegramHandlersFromAssemblyReflection_RegistersErrorOnlyHandlers()
    {
        var assembly = CompileAssembly(
            """
            using System;
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.ArchitectureTests;
            using TeleFlow.Telegram;

            namespace ReflectionRuntime;

            public sealed class ReflectionThrowingHandler
            {
                [Command("reflection-boom")]
                public Task Handle(MessageContext context)
                {
                    throw new ReflectionFailureException("boom");
                }
            }

            public sealed class ReflectionErrorOnlyHandler
            {
                [Error<ReflectionFailureException>]
                public TelegramErrorHandlingResult Handle(
                    MessageContext context,
                    ReflectionFailureException exception,
                    ReflectionRegistrationProbe probe)
                {
                    probe.Events.Add($"error:{context.GetType().Name}:{exception.Message}");
                    return TelegramErrorHandlingResult.Handled;
                }
            }

            public sealed class ReflectionFailureException : Exception
            {
                public ReflectionFailureException(string message)
                    : base(message)
                {
                }
            }
            """);
        var probe = new ReflectionRegistrationProbe();
        var services = CreateBaseServices(probe);

        services.AddTelegramHandlersFromAssemblyReflection(assembly);

        await DispatchAsync(CreateServiceProvider(services), CreateMessageUpdate("/reflection-boom"));

        Assert.Equal(["error:MessageContext:boom"], probe.Events);
    }

    [Fact]
    public async Task AddTelegramHandlersFromAssemblyReflection_RegistersModuleScopedErrorHandlers()
    {
        var assembly = CompileAssembly(
            """
            using System;
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.ArchitectureTests;
            using TeleFlow.Telegram;

            namespace ReflectionRuntime;

            [TelegramModule("reflection-module")]
            public sealed class ReflectionModuleThrowingHandler
            {
                [Command("reflection-module-boom")]
                public Task Handle(MessageContext context)
                {
                    throw new ReflectionModuleFailureException("module boom");
                }
            }

            [TelegramModule("reflection-module")]
            public sealed class ReflectionModuleErrorHandler
            {
                [Error]
                public TelegramErrorHandlingResult Handle(Exception exception, ReflectionRegistrationProbe probe)
                {
                    probe.Events.Add($"module-error:{exception.Message}");
                    return TelegramErrorHandlingResult.Handled;
                }
            }

            public sealed class ReflectionGlobalErrorHandler
            {
                [Error<ReflectionModuleFailureException>]
                public TelegramErrorHandlingResult Handle(
                    ReflectionModuleFailureException exception,
                    ReflectionRegistrationProbe probe)
                {
                    probe.Events.Add($"global-error:{exception.Message}");
                    return TelegramErrorHandlingResult.Handled;
                }
            }

            public sealed class ReflectionModuleFailureException : Exception
            {
                public ReflectionModuleFailureException(string message)
                    : base(message)
                {
                }
            }
            """);
        var probe = new ReflectionRegistrationProbe();
        var services = CreateBaseServices(probe);

        services.AddTelegramHandlersFromAssemblyReflection(assembly);

        await DispatchAsync(CreateServiceProvider(services), CreateMessageUpdate("/reflection-module-boom"));

        Assert.Equal(["module-error:module boom"], probe.Events);
    }

    [Fact]
    public async Task AddTelegramHandlersFromAssemblyReflection_RegistersRepeatedRouteAttributes()
    {
        var assembly = CompileAssembly(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.ArchitectureTests;
            using TeleFlow.Telegram;

            namespace ReflectionRuntime;

            public sealed class ReflectionRepeatedRouteHandler
            {
                [Command("reflection-one")]
                [Command("reflection-two")]
                public Task Handle(MessageContext context, ReflectionRegistrationProbe probe)
                {
                    probe.Events.Add($"repeated:{context.TelegramMessage.Text}");
                    return Task.CompletedTask;
                }
            }
            """);
        var probe = new ReflectionRegistrationProbe();
        var services = CreateBaseServices(probe);

        services.AddTelegramHandlersFromAssemblyReflection(assembly);
        await using var serviceProvider = CreateServiceProvider(services);

        await DispatchAsync(serviceProvider, CreateMessageUpdate("/reflection-one"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("/reflection-two"));

        Assert.Equal(["repeated:/reflection-one", "repeated:/reflection-two"], probe.Events);
    }

    [Fact]
    public async Task AddTelegramHandlersFromAssemblyReflection_RegistersRepeatedClassRouteAttributes()
    {
        var assembly = CompileAssembly(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.ArchitectureTests;
            using TeleFlow.Telegram;

            namespace ReflectionRuntime;

            [Text("reflection-class-one")]
            [Text("reflection-class-two")]
            public sealed class ReflectionRepeatedClassRouteHandler : MessageHandler
            {
                public Task HandleAsync(MessageContext context, ReflectionRegistrationProbe probe)
                {
                    probe.Events.Add($"class-repeated:{context.TelegramMessage.Text}");
                    return Task.CompletedTask;
                }
            }
            """);
        var probe = new ReflectionRegistrationProbe();
        var services = CreateBaseServices(probe);

        services.AddTelegramHandlersFromAssemblyReflection(assembly);
        await using var serviceProvider = CreateServiceProvider(services);

        await DispatchAsync(serviceProvider, CreateMessageUpdate("reflection-class-one"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("reflection-class-two"));

        Assert.Equal(["class-repeated:reflection-class-one", "class-repeated:reflection-class-two"], probe.Events);
    }

    [Fact]
    public async Task AddTelegramHandlersFromAssemblyReflection_RegistersClassBasedHandlers()
    {
        var assembly = CompileAssembly(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.ArchitectureTests;
            using TeleFlow.Telegram;

            namespace ReflectionRuntime;

            [Command("reflection-class")]
            public sealed class ReflectionClassBasedHandler : MessageHandler
            {
                public Task HandleAsync(MessageContext context, ReflectionRegistrationProbe probe)
                {
                    probe.Events.Add($"class:{context.TelegramMessage.Text}");
                    return Task.CompletedTask;
                }
            }
            """);
        var probe = new ReflectionRegistrationProbe();
        var services = CreateBaseServices(probe);

        services.AddTelegramHandlersFromAssemblyReflection(assembly);

        await DispatchAsync(CreateServiceProvider(services), CreateMessageUpdate("/reflection-class"));

        Assert.Equal(["class:/reflection-class"], probe.Events);
    }

    [Fact]
    public async Task AddTelegramHandlersFromAssemblyReflection_AppliesBuiltInFilterMetadata()
    {
        var assembly = CompileAssembly(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.ArchitectureTests;
            using TeleFlow.Telegram;

            namespace ReflectionRuntime;

            public sealed class AReflectionFilteredMessageHandler
            {
                [Message]
                [ChatType(TelegramChatType.Private)]
                [HasText]
                public Task Handle(MessageContext context, ReflectionRegistrationProbe probe)
                {
                    probe.Events.Add($"filtered-message:{context.TelegramMessage.Text}");
                    return Task.CompletedTask;
                }
            }

            public sealed class BReflectionFilteredCallbackHandler
            {
                [Callback]
                [ChatType(TelegramChatType.Private)]
                [CallbackDataPrefix("admin:")]
                public Task Handle(CallbackQueryContext context, ReflectionRegistrationProbe probe)
                {
                    probe.Events.Add($"filtered-callback:{context.TelegramCallbackQuery.Data}");
                    return Task.CompletedTask;
                }
            }

            public sealed class ZReflectionFallbackMessageHandler
            {
                [Message]
                public Task Handle(MessageContext context, ReflectionRegistrationProbe probe)
                {
                    probe.Events.Add($"fallback-message:{context.TelegramMessage.Text}");
                    return Task.CompletedTask;
                }
            }

            public sealed class ZReflectionFallbackCallbackHandler
            {
                [Callback]
                public Task Handle(CallbackQueryContext context, ReflectionRegistrationProbe probe)
                {
                    probe.Events.Add($"fallback-callback:{context.TelegramCallbackQuery.Data}");
                    return Task.CompletedTask;
                }
            }
            """);
        var probe = new ReflectionRegistrationProbe();
        var services = CreateBaseServices(probe);

        services.AddTelegramHandlersFromAssemblyReflection(assembly);
        await using var serviceProvider = CreateServiceProvider(services);

        await DispatchAsync(serviceProvider, CreateMessageUpdate("group", chatType: "group"));
        await DispatchAsync(serviceProvider, CreateMessageUpdate("private", chatType: "private"));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate("admin:no-message"));
        await DispatchAsync(serviceProvider, CreateCallbackUpdate("admin:private", includeMessage: true));
        await DispatchAsync(
            serviceProvider,
            CreateCallbackUpdate("admin:group", includeMessage: true, configureMessage: message => message with
            {
                Chat = new Chat { Id = 100, Type = "group" }
            }));

        Assert.Equal(
            [
                "fallback-message:group",
                "filtered-message:private",
                "fallback-callback:admin:no-message",
                "filtered-callback:admin:private",
                "fallback-callback:admin:group"
            ],
            probe.Events);
    }

    [Fact]
    public async Task AddTelegramHandlersFromAssemblyReflection_AppliesChatMemberTransitionMetadata()
    {
        var assembly = CompileAssembly(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.ArchitectureTests;
            using TeleFlow.Telegram;

            namespace ReflectionRuntime;

            public sealed class AReflectionPromotedChatMemberHandler
            {
                [ChatMemberUpdated]
                [ChatMemberTransition(TelegramMemberTransition.Promoted)]
                public Task Handle(ChatMemberUpdatedContext context, ReflectionRegistrationProbe probe)
                {
                    probe.Events.Add("promoted");
                    return Task.CompletedTask;
                }
            }

            public sealed class BReflectionDemotedChatMemberHandler
            {
                [ChatMemberUpdated]
                [ChatMemberTransition(TelegramMemberTransition.Demoted)]
                public Task Handle(ChatMemberUpdatedContext context, ReflectionRegistrationProbe probe)
                {
                    probe.Events.Add("demoted");
                    return Task.CompletedTask;
                }
            }

            public sealed class ZReflectionAnyChatMemberHandler
            {
                [ChatMemberUpdated]
                public Task Handle(ChatMemberUpdatedContext context, ReflectionRegistrationProbe probe)
                {
                    probe.Events.Add("any");
                    return Task.CompletedTask;
                }
            }
            """);
        var probe = new ReflectionRegistrationProbe();
        var services = CreateBaseServices(probe);

        services.AddTelegramHandlersFromAssemblyReflection(assembly);
        await using var serviceProvider = CreateServiceProvider(services);

        await DispatchAsync(
            serviceProvider,
            CreateChatMemberUpdate(
                oldChatMember: LeftChatMember(),
                newChatMember: AdministratorChatMember()));
        await DispatchAsync(
            serviceProvider,
            CreateChatMemberUpdate(
                oldChatMember: AdministratorChatMember(),
                newChatMember: MemberChatMember()));
        await DispatchAsync(
            serviceProvider,
            CreateChatMemberUpdate(
                oldChatMember: LeftChatMember(),
                newChatMember: MemberChatMember()));

        Assert.Equal(["promoted", "demoted", "any"], probe.Events);
    }

    [Fact]
    public void AddTelegramHandlersFromAssemblyReflection_FailsClearlyWhenAssemblyTypesCannotBeLoaded()
    {
        var services = CreateBaseServices(new ReflectionRegistrationProbe());

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandlersFromAssemblyReflection(new ThrowingTypesAssembly()));

        Assert.Contains("Could not load Telegram handler types from assembly", exception.Message);
        Assert.Contains("missing dependency", exception.Message);
    }

    [Fact]
    public void GeneratedAndReflectionAssemblyRegistration_CannotBeMixed()
    {
        var assembly = CompileAssembly(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace ReflectionRuntime;

            public sealed class GeneratedMixedHandler
            {
                [Command("generated-mixed")]
                public Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """,
            runGenerator: true);
        var services = CreateBaseServices(new ReflectionRegistrationProbe());

        services.AddTelegramHandlersFromAssembly(assembly);
        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandlersFromAssemblyReflection(assembly));

        Assert.Contains("already registered", exception.Message);
        Assert.Contains("generated", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReflectionAndGeneratedAssemblyRegistration_CannotBeMixed()
    {
        var assembly = CompileAssembly(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace ReflectionRuntime;

            public sealed class ReflectionMixedHandler
            {
                [Command("reflection-mixed")]
                public Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """,
            runGenerator: true);
        var services = CreateBaseServices(new ReflectionRegistrationProbe());

        services.AddTelegramHandlersFromAssemblyReflection(assembly);
        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandlersFromAssembly(assembly));

        Assert.Contains("already registered", exception.Message);
        Assert.Contains("reflection", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DirectAndReflectionSameHandlerType_CannotBeMixed()
    {
        var assembly = CompileAssembly(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace ReflectionRuntime;

            public sealed class DirectReflectionMixedHandler
            {
                [Command("direct-reflection-mixed")]
                public Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);
        var handlerType = assembly.GetType("ReflectionRuntime.DirectReflectionMixedHandler", throwOnError: true)!;
        var services = CreateBaseServices(new ReflectionRegistrationProbe());

        AddTelegramHandler(services, handlerType);
        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTelegramHandlersFromAssemblyReflection(assembly));

        Assert.Contains("already registered", exception.Message);
        Assert.Contains(handlerType.FullName!, exception.Message);
    }

    [Fact]
    public void ReflectionAndDirectSameHandlerType_CannotBeMixed()
    {
        var assembly = CompileAssembly(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace ReflectionRuntime;

            public sealed class ReflectionDirectMixedHandler
            {
                [Command("reflection-direct-mixed")]
                public Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);
        var handlerType = assembly.GetType("ReflectionRuntime.ReflectionDirectMixedHandler", throwOnError: true)!;
        var services = CreateBaseServices(new ReflectionRegistrationProbe());

        services.AddTelegramHandlersFromAssemblyReflection(assembly);
        var exception = Assert.Throws<InvalidOperationException>(
            () => AddTelegramHandler(services, handlerType));

        Assert.Contains("already registered", exception.Message);
        Assert.Contains(handlerType.FullName!, exception.Message);
    }

    private static IServiceCollection CreateBaseServices(ReflectionRegistrationProbe probe)
    {
        var services = new ServiceCollection();
        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddSingleton(probe);
        return services;
    }

    private static ServiceProvider CreateServiceProvider(IServiceCollection services)
    {
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
            .GetRequiredService<IUpdateDispatcher>()
            .DispatchAsync(context, cancellationToken);
    }

    private static Update CreateMessageUpdate(
        string text,
        string chatType = "private")
    {
        return new Update
        {
            UpdateId = 1,
            Message = new Message
            {
                MessageId = 10,
                Date = 0,
                Chat = new Chat { Id = 100, Type = chatType },
                From = new User { Id = 5, IsBot = false, FirstName = "User" },
                Text = text
            }
        };
    }

    private static Update CreateCallbackUpdate(
        string data,
        bool includeMessage = false,
        Func<Message, Message>? configureMessage = null)
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
                From = new User { Id = 5, IsBot = false, FirstName = "User" },
                ChatInstance = "chat-instance",
                Data = data,
                Message = includeMessage ? MaybeInaccessibleMessage.From(message) : null
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
                Chat = new Chat { Id = 100, Type = "supergroup" },
                From = new User { Id = 42, IsBot = false, FirstName = "Actor" },
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

    private static Assembly CompileAssembly(string source, bool runGenerator = false)
    {
        var references = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedAssemblies
            ? trustedAssemblies
                .Split(Path.PathSeparator)
                .Select(static path => MetadataReference.CreateFromFile(path))
                .Cast<MetadataReference>()
                .ToList()
            : [];

        references.Add(MetadataReference.CreateFromFile(typeof(CommandAttribute).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(State).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(MessageContext).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(ReflectionRegistrationProbe).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            $"ReflectionRegistrationTests_{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Compilation effectiveCompilation = compilation;

        if (runGenerator)
        {
            var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
            driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out effectiveCompilation,
                out var generatorDiagnostics);

            Assert.Empty(generatorDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        using var stream = new MemoryStream();
        var result = effectiveCompilation.Emit(stream);

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Diagnostics
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(static diagnostic => diagnostic.ToString())));

        return Assembly.Load(stream.ToArray());
    }

    private static void AddTelegramHandler(IServiceCollection services, Type handlerType)
    {
        var method = typeof(TelegramServiceCollectionExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(static candidate => candidate is
            {
                Name: nameof(TelegramServiceCollectionExtensions.AddTelegramHandler),
                IsGenericMethodDefinition: true
            });

        try
        {
            method.MakeGenericMethod(handlerType).Invoke(null, [services]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private sealed class ThrowingTypesAssembly : Assembly
    {
        public override string FullName => "BrokenReflectionAssembly";

        public override Type[] GetTypes()
        {
            throw new ReflectionTypeLoadException(
                [],
                [new FileNotFoundException("missing dependency")]);
        }
    }
}

public sealed class ReflectionRegistrationProbe
{
    public List<string> Events { get; } = [];
}
