using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeleFlow.Annotations;
using TeleFlow.Framework.Callbacks;
using TeleFlow.Framework.Dispatching;
using TeleFlow.Framework.States;
using TeleFlow.Framework.Updates;
using TeleFlow.Generators;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.ArchitectureTests;

public sealed class TelegramHandlerGeneratorTests
{
    [Fact]
    public void Generator_EmitsRegistrarMetadataAndDirectInvokers()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            [TelegramModule("admin")]
            [State("type-state")]
            public sealed class MyHandlers
            {
                [Command("start")]
                public Task Start(MessageContext context, HandlerProbe probe, CancellationToken cancellationToken)
                {
                    return Task.CompletedTask;
                }

                [Message]
                [Text("hello", TextMatchMode.StartsWith, false)]
                [State("method-state")]
                public ValueTask Message(MessageContext context, HandlerProbe probe)
                {
                    return ValueTask.CompletedTask;
                }

                [Callback<DeletePayload>]
                [AutoAnswerCallback("Deleted", ShowAlert = true)]
                public Task Callback(CallbackQueryContext context, DeletePayload payload, HandlerProbe probe)
                {
                    return Task.CompletedTask;
                }
            }

            public sealed class HandlerProbe { }
            public sealed record DeletePayload(long Id);
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var generatedTree = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal));
        var generatedSource = generatedTree.ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("TelegramGeneratedHandlersAttribute", generatedSource);
        Assert.Contains("TelegramGeneratedHandlerKind.Command", generatedSource);
        Assert.Contains("\"admin\"", generatedSource);
        Assert.Contains("\"start\"", generatedSource);
        Assert.Contains("TelegramGeneratedHandlerKind.Message", generatedSource);
        Assert.Contains("TelegramGeneratedHandlerKind.Callback", generatedSource);
        Assert.Contains("typeof(global::Bot.DeletePayload)", generatedSource);
        Assert.Contains("TelegramGeneratedHandlerParameterKind.CallbackPayload", generatedSource);
        Assert.Contains("TelegramGeneratedAutoAnswerCallbackDescriptor", generatedSource);
        Assert.Contains("\"Deleted\"", generatedSource);
        Assert.Contains("showAlert: true", generatedSource);
        Assert.Contains("\"type-state\"", generatedSource);
        Assert.Contains("\"method-state\"", generatedSource);
        Assert.Contains("TextMatchMode)1", generatedSource);
        Assert.Contains("Invoke_0", generatedSource);
        Assert.Contains("services.GetService(typeof(global::Bot.MyHandlers))", generatedSource);
    }

    [Fact]
    public void Generator_EmitsCallbackDataCodecRegistrarAndDirectCodecs()
    {
        var compilation = CreateCompilation(
            """
            using TeleFlow.Annotations;

            namespace Bot;

            [CallbackData("ticket")]
            public sealed record TicketAction(long Id, string Action, bool Confirmed, TicketActionKind Kind);

            public enum TicketActionKind
            {
                Take,
                Resolve
            }
            """);

        var generatedCompilation = RunGenerator(compilation, out var diagnostics);
        var generatedTree = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal));
        var generatedSource = generatedTree.ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("TelegramGeneratedCallbackDataCodecsAttribute", generatedSource);
        Assert.Contains("ITelegramGeneratedCallbackDataCodecRegistrar", generatedSource);
        Assert.Contains("RegisterCallbackDataCodec", generatedSource);
        Assert.Contains("typeof(global::Bot.TicketAction)", generatedSource);
        Assert.Contains("\"ticket\"", generatedSource);
        Assert.Contains("PackCallbackData_0", generatedSource);
        Assert.Contains("UnpackCallbackData_0", generatedSource);
        Assert.Contains("MatchesCallbackData_0", generatedSource);
        Assert.DoesNotContain("PropertyInfo", generatedSource);
        Assert.DoesNotContain("ConstructorInfo", generatedSource);
        Assert.DoesNotContain("Activator.CreateInstance", generatedSource);
    }

    [Fact]
    public async Task Generator_CallbackDataCodecsRunThroughKeyboardSerializerAndDispatcher()
    {
        var runId = Guid.NewGuid().ToString("N");
        GeneratedErrorRuntimeProbe.Clear(runId);
        var compilation = CreateCompilation(
            $$"""
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.ArchitectureTests;
            using TeleFlow.Telegram;

            namespace GeneratedCallbackCodecs;

            [CallbackData("ticket")]
            public sealed record TicketAction(long Id, string Action, bool Confirmed, TicketActionKind Kind);

            public enum TicketActionKind
            {
                Take,
                Resolve
            }

            public sealed class CallbackHandlers
            {
                [Callback<TicketAction>]
                public Task Handle(CallbackQueryContext context, TicketAction payload)
                {
                    GeneratedErrorRuntimeProbe.Record(
                        "{{runId}}",
                        $"callback:{payload.Id}:{payload.Action}:{payload.Confirmed}:{payload.Kind}");
                    return Task.CompletedTask;
                }

                [Callback]
                public Task Raw(CallbackQueryContext context)
                {
                    GeneratedErrorRuntimeProbe.Record(
                        "{{runId}}",
                        $"raw:{context.TelegramCallbackQuery.Data}");
                    return Task.CompletedTask;
                }
            }
            """);
        var generatedCompilation = RunGenerator(compilation, out var diagnostics);
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);

        var assembly = EmitAndLoad(generatedCompilation);
        var payloadType = assembly.GetType("GeneratedCallbackCodecs.TicketAction", throwOnError: true)!;
        var payloadKindType = assembly.GetType("GeneratedCallbackCodecs.TicketActionKind", throwOnError: true)!;
        var takeKind = Enum.Parse(payloadKindType, "Take");
        var payload = Activator.CreateInstance(payloadType, 42L, "a:b%z", true, takeKind)!;
        var services = new ServiceCollection();
        var loggerFactory = new RecordingLoggerFactory();
        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddSingleton<ILoggerFactory>(loggerFactory);
        services.AddTelegramHandlersFromAssembly(assembly);
        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var cancellation = new CancellationTokenSource();

        var keyboard = InlineKeyboardBuilder.Create()
            .Button("Take", payload)
            .Build();
        var callbackData = keyboard.InlineKeyboard[0][0].CallbackData;
        var serializer = serviceProvider.GetRequiredService<ICallbackDataSerializer>();

        Assert.Equal("ticket:42:a%3Ab%25z:true:Take", callbackData);
        Assert.Equal(callbackData, serializer.Serialize((dynamic)payload));

        await DispatchAsync(
            serviceProvider,
            CreateCallbackUpdate(callbackData),
            cancellation.Token);

        Assert.Equal(
            ["callback:42:a:b%z:True:Take"],
            GeneratedErrorRuntimeProbe.GetEvents(runId));

        await DispatchAsync(
            serviceProvider,
            CreateCallbackUpdate("ticket:not-long:stale:true:Take"),
            cancellation.Token);

        Assert.Equal(
            ["callback:42:a:b%z:True:Take", "raw:ticket:not-long:stale:true:Take"],
            GeneratedErrorRuntimeProbe.GetEvents(runId));
        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Warning &&
                     entry.Category == "TeleFlow.Telegram.Internal.Handlers.TelegramHandlerSelector" &&
                     entry.Message.Contains("Telegram callback data failed to deserialize", StringComparison.Ordinal) &&
                     entry.Message.Contains("TicketAction", StringComparison.Ordinal));
    }

    [Fact]
    public void Generator_EmitsErrorHandlerMetadataAndDirectInvokers()
    {
        var compilation = CreateCompilation(
            """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            [TelegramModule("orders")]
            public sealed class ErrorHandlers
            {
                [Error<InvalidOperationException>]
                public Task<TelegramErrorHandlingResult> Invalid(
                    TelegramErrorContext error,
                    MessageContext context,
                    InvalidOperationException exception,
                    long orderId,
                    HandlerProbe probe,
                    CancellationToken cancellationToken)
                {
                    return Task.FromResult(TelegramErrorHandlingResult.Handled);
                }

                [Error]
                public TelegramErrorHandlingResult Any(Exception exception)
                {
                    return TelegramErrorHandlingResult.Unhandled;
                }
            }

            public sealed class HandlerProbe { }
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var generatedTree = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal));
        var generatedSource = generatedTree.ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("registry.RegisterErrorHandler", generatedSource);
        Assert.Contains("TelegramGeneratedErrorHandlerDescriptor", generatedSource);
        Assert.Contains("typeof(global::System.InvalidOperationException)", generatedSource);
        Assert.Contains("\"orders\"", generatedSource);
        Assert.Contains("TelegramGeneratedErrorHandlerParameterKind.ErrorContext", generatedSource);
        Assert.Contains("TelegramGeneratedErrorHandlerParameterKind.TelegramContext", generatedSource);
        Assert.Contains("TelegramGeneratedErrorHandlerParameterKind.Exception", generatedSource);
        Assert.Contains("TelegramGeneratedErrorHandlerParameterKind.RouteValue", generatedSource);
        Assert.Contains("TelegramGeneratedErrorHandlerParameterKind.Service", generatedSource);
        Assert.Contains("TelegramGeneratedErrorHandlerParameterKind.CancellationToken", generatedSource);
        Assert.Contains("InvokeError_0", generatedSource);
        Assert.Contains("global::System.Threading.Tasks.ValueTask<global::TeleFlow.Telegram.TelegramErrorHandlingResult>", generatedSource);
        Assert.Contains("services.GetService(typeof(global::Bot.ErrorHandlers))", generatedSource);
    }

    [Fact]
    public async Task Generator_ErrorHandlerOutputRunsThroughGeneratedAssemblyRegistration()
    {
        var runId = Guid.NewGuid().ToString("N");
        GeneratedErrorRuntimeProbe.Clear(runId);
        var compilation = CreateCompilation(
            $$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.ArchitectureTests;
            using TeleFlow.Telegram;

            namespace GeneratedRuntime;

            public sealed class ThrowingHandlers
            {
                [Command("generated-error-smoke")]
                public Task Handle(MessageContext context)
                {
                    throw new GeneratedErrorSmokeException("smoke");
                }
            }

            public sealed class ErrorHandlers
            {
                [Error<GeneratedErrorSmokeException>]
                public ValueTask<TelegramErrorHandlingResult> Handle(
                    TelegramErrorContext error,
                    MessageContext context,
                    GeneratedErrorSmokeException exception,
                    CancellationToken cancellationToken)
                {
                    GeneratedErrorRuntimeProbe.Record(
                        "{{runId}}",
                        $"handled:{context.GetType().Name}:{error.HandlerMethodName}:{exception.Message}:{cancellationToken.CanBeCanceled}");
                    return ValueTask.FromResult(TelegramErrorHandlingResult.Handled);
                }
            }

            public sealed class GeneratedErrorSmokeException : Exception
            {
                public GeneratedErrorSmokeException(string message)
                    : base(message)
                {
                }
            }
            """);
        var generatedCompilation = RunGenerator(compilation, out var diagnostics);
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        var assembly = EmitAndLoad(generatedCompilation);
        var services = new ServiceCollection();
        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddTelegramHandlersFromAssembly(assembly);
        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var cancellation = new CancellationTokenSource();

        await DispatchAsync(
            serviceProvider,
            CreateMessageUpdate("/generated-error-smoke"),
            cancellation.Token);

        Assert.Equal(
            ["handled:MessageContext:Handle:smoke:True"],
            GeneratedErrorRuntimeProbe.GetEvents(runId));
    }

    [Fact]
    public void Generator_EmitsTemplateRegexRoutesAndSharedInvoker()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            public sealed class RouteHandlers
            {
                [Command("help")]
                [Text("help")]
                public Task Help(MessageContext context)
                {
                    return Task.CompletedTask;
                }

                [TextTemplate("order {orderId:long}")]
                [CommandRegex(@"^order (?<orderId>\d+)$")]
                public Task Order(MessageContext context, long orderId)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var generatedSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            .ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("TelegramGeneratedRouteKind.CommandExact", generatedSource);
        Assert.Contains("TelegramGeneratedRouteKind.TextExact", generatedSource);
        Assert.Contains("TelegramGeneratedRouteKind.TextTemplate", generatedSource);
        Assert.Contains("TelegramGeneratedRouteKind.CommandRegex", generatedSource);
        Assert.Contains("TelegramGeneratedHandlerParameterKind.RouteValue", generatedSource);
        Assert.Contains("Invoke_0", generatedSource);
        Assert.Contains("Invoke_1", generatedSource);
        Assert.DoesNotContain("Invoke_2", generatedSource);
    }

    [Fact]
    public void Generator_EmitsOptionalTemplateRouteValueMetadata()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            public sealed class RouteHandlers
            {
                [TextTemplate("order {orderId:long?}")]
                public Task Order(MessageContext context, long? orderId)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var generatedSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            .ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("new global::TeleFlow.Telegram.TelegramGeneratedRouteValueDescriptor(\"orderId\", typeof(long), true)", generatedSource);
    }

    [Fact]
    public void Generator_DoesNotEmitNonCanonicalOptionalTemplateRoute()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            public sealed class RouteHandlers
            {
                [TextTemplate("order {orderId?:long}")]
                public Task Order(MessageContext context, long? orderId)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out _);

        var generatedSource = generatedCompilation.SyntaxTrees
            .SingleOrDefault(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            ?.ToString() ?? string.Empty;

        Assert.DoesNotContain("TelegramGeneratedRouteKind.TextTemplate", generatedSource);
        Assert.DoesNotContain("TelegramGeneratedRouteValueDescriptor", generatedSource);
    }

    [Fact]
    public void Generator_EmitsChatMemberRouteAndTransitionMetadata()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            public sealed class ChatMemberHandlers
            {
                [ChatMemberUpdated]
                [ChatMemberTransition(TelegramMemberTransition.Join)]
                public Task Join(ChatMemberUpdatedContext context)
                {
                    return Task.CompletedTask;
                }

                [MyChatMemberUpdated]
                [ChatMemberChanged(TelegramMemberStatusSet.IsNotMember, TelegramMemberStatusSet.IsMember)]
                public Task BotJoined(ChatMemberUpdatedContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var generatedSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            .ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("TelegramGeneratedHandlerKind.ChatMember", generatedSource);
        Assert.Contains("TelegramGeneratedRouteKind.ChatMemberUpdated", generatedSource);
        Assert.Contains("TelegramGeneratedRouteKind.MyChatMemberUpdated", generatedSource);
        Assert.Contains("TelegramGeneratedChatMemberTransitionDescriptor", generatedSource);
        Assert.Contains("ChatMemberUpdatedContext", generatedSource);
        Assert.Contains("Invoke_0", generatedSource);
        Assert.Contains("Invoke_1", generatedSource);
    }

    [Fact]
    public void Generator_EmitsAllChatMemberTransitionMappings()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            public sealed class ChatMemberHandlers
            {
                [ChatMemberUpdated]
                [ChatMemberTransition(TelegramMemberTransition.Join)]
                public Task Join(ChatMemberUpdatedContext context) => Task.CompletedTask;

                [ChatMemberUpdated]
                [ChatMemberTransition(TelegramMemberTransition.Leave)]
                public Task Leave(ChatMemberUpdatedContext context) => Task.CompletedTask;

                [ChatMemberUpdated]
                [ChatMemberTransition(TelegramMemberTransition.Promoted)]
                public Task Promoted(ChatMemberUpdatedContext context) => Task.CompletedTask;

                [ChatMemberUpdated]
                [ChatMemberTransition(TelegramMemberTransition.Demoted)]
                public Task Demoted(ChatMemberUpdatedContext context) => Task.CompletedTask;
            }
            """);

        var generatedCompilation = RunGenerator(compilation, out var diagnostics);
        var generatedSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            .ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("(global::TeleFlow.Annotations.TelegramMemberStatusSet)112, (global::TeleFlow.Annotations.TelegramMemberStatusSet)15", generatedSource);
        Assert.Contains("(global::TeleFlow.Annotations.TelegramMemberStatusSet)15, (global::TeleFlow.Annotations.TelegramMemberStatusSet)112", generatedSource);
        Assert.Contains("(global::TeleFlow.Annotations.TelegramMemberStatusSet)124, (global::TeleFlow.Annotations.TelegramMemberStatusSet)3", generatedSource);
        Assert.Contains("(global::TeleFlow.Annotations.TelegramMemberStatusSet)3, (global::TeleFlow.Annotations.TelegramMemberStatusSet)124", generatedSource);
    }

    [Fact]
    public void Generator_EmitsClassBasedHandlerMetadata()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            [Command("classstart")]
            public sealed class StartHandler : MessageHandler
            {
                public Task HandleAsync(MessageContext context, HandlerProbe probe)
                {
                    return Task.CompletedTask;
                }
            }

            [TextTemplate("class order {orderId:long}")]
            public sealed class OrderHandler : MessageHandler
            {
                public Task HandleAsync(MessageContext context, long orderId, HandlerProbe probe)
                {
                    return Task.CompletedTask;
                }
            }

            [Callback<DeletePayload>]
            public sealed class DeleteHandler : CallbackHandler<DeletePayload>
            {
                public Task HandleAsync(CallbackQueryContext context, DeletePayload payload, HandlerProbe probe)
                {
                    return Task.CompletedTask;
                }
            }

            [ChatMemberUpdated]
            [ChatMemberTransition(TelegramMemberTransition.Join)]
            public sealed class JoinHandler : ChatMemberUpdateHandler
            {
                public Task HandleAsync(ChatMemberUpdatedContext context, HandlerProbe probe)
                {
                    return Task.CompletedTask;
                }
            }

            public sealed class HandlerProbe { }
            public sealed record DeletePayload(long Id);
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var generatedSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            .ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("typeof(global::Bot.StartHandler)", generatedSource);
        Assert.Contains("typeof(global::Bot.OrderHandler)", generatedSource);
        Assert.Contains("typeof(global::Bot.DeleteHandler)", generatedSource);
        Assert.Contains("typeof(global::Bot.JoinHandler)", generatedSource);
        Assert.Contains("\"HandleAsync\"", generatedSource);
        Assert.Contains("\"classstart\"", generatedSource);
        Assert.Contains("TelegramGeneratedRouteKind.TextTemplate", generatedSource);
        Assert.Contains("TelegramGeneratedHandlerParameterKind.RouteValue", generatedSource);
        Assert.Contains("typeof(global::Bot.DeletePayload)", generatedSource);
        Assert.Contains("TelegramGeneratedHandlerParameterKind.CallbackPayload", generatedSource);
        Assert.Contains("TelegramGeneratedRouteKind.ChatMemberUpdated", generatedSource);
        Assert.Contains("TelegramGeneratedChatMemberTransitionDescriptor", generatedSource);
    }

    [Fact]
    public void Generator_EmitsTelegramRoleRequirementMetadata()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            [RequireTelegramRole(TelegramMemberStatusSet.IsAdmin)]
            public sealed class RoleHandlers
            {
                [Command("admin")]
                [RequireTelegramRole(TelegramMemberStatusSet.Creator)]
                public Task Admin(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var generatedSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            .ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("TelegramGeneratedRoleRequirementDescriptor", generatedSource);
        Assert.Contains("(global::TeleFlow.Annotations.TelegramMemberStatusSet)3", generatedSource);
        Assert.Contains("(global::TeleFlow.Annotations.TelegramMemberStatusSet)1", generatedSource);
    }

    [Fact]
    public void Generator_DoesNotEmitInvalidCommandPrefixOrPrefixedTemplateRoutes()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            public sealed class RouteHandlers
            {
                [Command("valid")]
                public Task Valid(MessageContext context)
                {
                    return Task.CompletedTask;
                }

                [Command("bad", Prefixes = new[] { "" })]
                public Task BadPrefix(MessageContext context)
                {
                    return Task.CompletedTask;
                }

                [CommandTemplate("/ban {id:int}")]
                public Task PrefixedTemplate(MessageContext context, int id)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var generatedSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            .ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("Valid", generatedSource);
        Assert.DoesNotContain("BadPrefix", generatedSource);
        Assert.DoesNotContain("PrefixedTemplate", generatedSource);
    }

    [Fact]
    public void Generator_EmitsBuiltInFilterMetadata()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            [ChatType(TelegramChatType.Private)]
            [ChatId(-1001234567890)]
            [ChatUsername("@Group")]
            public sealed class FilteredHandlers
            {
                [Message]
                [HasText]
                [HasCaption]
                [HasVideo]
                [FromBot(false)]
                [FromPremiumUser]
                [IsReply]
                [FromUser(5)]
                [MessageThreadId(42)]
                [HasMessageThread]
                public Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }

            }

            public sealed class CallbackFilteredHandlers
            {
                [Callback]
                [ChatId(100)]
                [HasCallbackData]
                [CallbackDataPrefix("admin:")]
                public Task Handle(CallbackQueryContext context)
                {
                    return Task.CompletedTask;
                }
            }

            public sealed class TrimmedCallbackFilteredHandlers
            {
                [Callback]
                [CallbackDataPrefix(" trimmed: ")]
                public Task Handle(CallbackQueryContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var generatedSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            .ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("TelegramGeneratedFilterDescriptor", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.ChatType", generatedSource);
        Assert.Contains("\"private\"", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.ChatId", generatedSource);
        Assert.Contains("-1001234567890L", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.ChatUsername", generatedSource);
        Assert.Contains("\"Group\"", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.HasText", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.HasCaption", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.HasVideo", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.FromBot", generatedSource);
        Assert.Contains("\"False\"", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.FromPremiumUser", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.IsReply", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.FromUser", generatedSource);
        Assert.Contains("5L", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.MessageThreadId", generatedSource);
        Assert.Contains("42L", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.HasMessageThread", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.HasCallbackData", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.CallbackDataPrefix", generatedSource);
        Assert.Contains("\"admin:\"", generatedSource);
        Assert.Contains("\"trimmed:\"", generatedSource);
        Assert.DoesNotContain("\" trimmed: \"", generatedSource);
    }

    [Fact]
    public void Generator_EmitsAllChatTypeFilterMappings()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            [ChatType(
                TelegramChatType.Private,
                TelegramChatType.Group,
                TelegramChatType.Supergroup,
                TelegramChatType.Channel,
                TelegramChatType.Sender)]
            public sealed class FilteredHandlers
            {
                [Message]
                public Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var generatedCompilation = RunGenerator(compilation, out var diagnostics);
        var generatedSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            .ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("TelegramGeneratedFilterKind.ChatType", generatedSource);
        Assert.Contains("\"private\"", generatedSource);
        Assert.Contains("\"group\"", generatedSource);
        Assert.Contains("\"supergroup\"", generatedSource);
        Assert.Contains("\"channel\"", generatedSource);
        Assert.Contains("\"sender\"", generatedSource);
    }

    [Fact]
    public void Generator_EmitsCustomFilterMetadata()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            [UseFilter<AllowUpdateFilter>]
            public sealed class FilteredHandlers
            {
                [Message]
                [UseFilter<AllowMessageFilter>]
                public Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
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

            public sealed class AllowMessageFilter : ITelegramFilter<MessageContext>
            {
                public ValueTask<bool> MatchesAsync(
                    MessageContext context,
                    CancellationToken cancellationToken = default)
                {
                    return ValueTask.FromResult(true);
                }
            }
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var generatedSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            .ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("new global::TeleFlow.Telegram.TelegramGeneratedFilterDescriptor(typeof(global::Bot.AllowUpdateFilter), typeof(global::TeleFlow.Telegram.TelegramUpdateContext))", generatedSource);
        Assert.Contains("new global::TeleFlow.Telegram.TelegramGeneratedFilterDescriptor(typeof(global::Bot.AllowMessageFilter), typeof(global::TeleFlow.Telegram.MessageContext))", generatedSource);
    }

    [Fact]
    public void Generator_EmitsParameterizedCustomFilterMetadata()
    {
        var compilation = CreateCompilation(
            """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            public sealed class FilteredHandlers
            {
                [Message]
                [RequireText("hello", IgnoreCase = true)]
                public Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }

            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
            public sealed class RequireTextAttribute : TelegramFilterAttribute<RequireTextFilter>
            {
                public RequireTextAttribute(string text)
                {
                    Text = text;
                }

                public string Text { get; }

                public bool IgnoreCase { get; set; }
            }

            public sealed class RequireTextFilter : ITelegramFilter<MessageContext, RequireTextAttribute>
            {
                public ValueTask<bool> MatchesAsync(
                    MessageContext context,
                    RequireTextAttribute attribute,
                    CancellationToken cancellationToken = default)
                {
                    return ValueTask.FromResult(true);
                }
            }
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var generatedSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            .ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("new global::TeleFlow.Telegram.TelegramGeneratedFilterDescriptor(", generatedSource);
        Assert.Contains("typeof(global::Bot.RequireTextFilter)", generatedSource);
        Assert.Contains("typeof(global::TeleFlow.Telegram.MessageContext)", generatedSource);
        Assert.Contains("new global::Bot.RequireTextAttribute(\"hello\")", generatedSource);
        Assert.Contains("IgnoreCase = true", generatedSource);
    }

    [Fact]
    public void Generator_EmitsInheritedBuiltInFilterMetadata()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            [ChatType(TelegramChatType.Private)]
            public abstract class PrivateHandlerBase
            {
            }

            [ReplyToBot]
            public abstract class ReplyHandlerBase : PrivateHandlerBase
            {
            }

            public sealed class FilteredHandlers : ReplyHandlerBase
            {
                [Message]
                [HasText]
                [HasSticker]
                public Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var generatedSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            .ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("TelegramGeneratedFilterKind.ChatType", generatedSource);
        Assert.Contains("\"private\"", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.HasText", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.HasSticker", generatedSource);
        Assert.Contains("TelegramGeneratedFilterKind.ReplyToBot", generatedSource);
    }

    [Fact]
    public void Generator_EmitsInheritedRouteMetadata()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            public abstract class BaseHandler
            {
                [Message]
                public virtual Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }

            public sealed class DerivedHandler : BaseHandler
            {
                public override Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var generatedSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            .ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("typeof(global::Bot.DerivedHandler)", generatedSource);
        Assert.Contains("TelegramGeneratedRouteKind.MessageAny", generatedSource);
    }

    [Fact]
    public async Task Analyzer_AllowsAbstractBaseRouteMetadataForInheritance()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public abstract class BaseHandler
            {
                [Message]
                public virtual Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }

            public sealed class DerivedHandler : BaseHandler
            {
                public override Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidHandlerTypeId);
    }

    [Fact]
    public async Task Analyzer_DoesNotReportInheritedHandlerMethodForOrdinarySubtype()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public abstract class BaseHandler
            {
                [Message]
                public virtual Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }

            public sealed class DerivedHandler : BaseHandler
            {
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InheritedHandlerMethodId);
    }

    [Fact]
    public async Task Analyzer_ReportsInheritedHandlerMethodForModuleCandidateWithoutOverride()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public abstract class BaseHandler
            {
                [Message]
                public virtual Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }

            [TelegramModule("derived")]
            public sealed class DerivedModule : BaseHandler
            {
            }
            """);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InheritedHandlerMethodId);
    }

    [Fact]
    public async Task Analyzer_ReportsInheritedHandlerMethodForHandlerCandidateWithoutOverride()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public abstract class BaseHandler
            {
                [Message]
                public virtual Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }

            public sealed class DerivedHandler : BaseHandler
            {
                [Command("start")]
                public Task Start(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InheritedHandlerMethodId);
    }

    [Fact]
    public async Task Analyzer_AllowsOverrideOfInheritedHandlerMethod()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public abstract class BaseHandler
            {
                [Message]
                public virtual Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }

            public sealed class DerivedHandler : BaseHandler
            {
                public override Task Handle(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InheritedHandlerMethodId);
    }

    [Fact]
    public void Generator_EmitsStateGroupsAndTypedStateMetadata()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Framework.States;
            using TeleFlow.Telegram;

            namespace Bot;

            [StateGroup("registration")]
            public sealed partial class RegistrationStates
            {
                public static partial State Name { get; }

                [StateValue("customAge")]
                public static partial State Age { get; }
            }

            public sealed class MyHandlers
            {
                [Message]
                [State<RegistrationStates>(nameof(RegistrationStates.Name))]
                public Task Name(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var stateGroupSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.StateGroups.g.cs", StringComparison.Ordinal))
            .ToString();
        var handlerSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            .ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("public static partial global::TeleFlow.Framework.States.State Name", stateGroupSource);
        Assert.Contains("\"registration:name\"", stateGroupSource);
        Assert.Contains("\"registration:customAge\"", stateGroupSource);
        Assert.Contains("\"registration:name\"", handlerSource);
    }

    [Fact]
    public void Generator_EmitsSceneStatesAndSceneStepMetadata()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Framework.States;
            using TeleFlow.Telegram;

            namespace Bot;

            [Scene("registration")]
            public sealed partial class RegistrationScene
            {
                public static partial State Name { get; }

                [StateValue("customAge")]
                public static partial State Age { get; }

                [Message]
                [SceneStep(nameof(Name))]
                public Task NameStep(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var stateGroupSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.StateGroups.g.cs", StringComparison.Ordinal))
            .ToString();
        var handlerSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            .ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("public static partial global::TeleFlow.Framework.States.State Name", stateGroupSource);
        Assert.Contains("\"registration:name\"", stateGroupSource);
        Assert.Contains("\"registration:customAge\"", stateGroupSource);
        Assert.Contains("\"registration:name\"", handlerSource);
        Assert.Contains("Invoke_0,", handlerSource);
        Assert.Contains("            \"registration\",", handlerSource);
    }

    [Fact]
    public void Generator_SkipsSceneStepWhenStateReferenceIsNotGeneratedStateProperty()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Framework.States;
            using TeleFlow.Telegram;

            namespace Bot;

            [Scene("registration")]
            public sealed partial class RegistrationScene
            {
                public static partial State Name { get; }

                private static State Hidden => State.Create("registration:hidden");

                [Message]
                [SceneStep(nameof(Hidden))]
                public Task HiddenStep(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var handlerSource = generatedCompilation.SyntaxTrees
            .FirstOrDefault(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal))
            ?.ToString() ?? string.Empty;
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.DoesNotContain("HiddenStep", handlerSource);
        Assert.DoesNotContain("registration:hidden", handlerSource);
    }

    [Fact]
    public void Generator_SkipsNonPartialStatePropertiesInGeneratedStateOutput()
    {
        var compilation = CreateCompilation(
            """
            using TeleFlow.Annotations;
            using TeleFlow.Framework.States;

            namespace Bot;

            [Scene("registration")]
            public sealed partial class RegistrationScene
            {
                public static partial State Name { get; }

                public static State Manual => State.Create("registration:manual");
            }
            """);

        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var diagnostics);

        var stateGroupSource = generatedCompilation.SyntaxTrees
            .Single(tree => tree.FilePath.EndsWith("TeleFlow.StateGroups.g.cs", StringComparison.Ordinal))
            .ToString();
        var errors = generatedCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Contains("\"registration:name\"", stateGroupSource);
        Assert.DoesNotContain("\"registration:manual\"", stateGroupSource);
        Assert.DoesNotContain("partial global::TeleFlow.Framework.States.State Manual", stateGroupSource);
    }

    [Theory]
    [InlineData("public void Handle(MessageContext context) { }", TelegramHandlerAnalyzer.UnsupportedReturnTypeId)]
    [InlineData("[Callback] public Task Handle(MessageContext context) => Task.CompletedTask;", TelegramHandlerAnalyzer.MultipleRouteAttributesId)]
    [InlineData("public Task Handle(CallbackQueryContext context) => Task.CompletedTask;", TelegramHandlerAnalyzer.InvalidContextParameterId)]
    [InlineData("public Task Handle(MessageContext context, CancellationToken first, CancellationToken second) => Task.CompletedTask;", TelegramHandlerAnalyzer.MultipleCancellationTokensId)]
    public async Task Analyzer_ReportsInvalidHandlerSignatures(string methodSource, string diagnosticId)
    {
        var source = $$"""
            using System.Threading;
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class InvalidHandler
            {
                [Message]
                {{methodSource}}
            }
            """;

        var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == diagnosticId);
    }

    [Fact]
    public async Task Analyzer_ReportsHandlerConstraintAttributesWithoutRoute()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Framework.States;
            using TeleFlow.Telegram;

            public sealed class RouteLessHandlers
            {
                [State("registration:name")]
                [HasText]
                public Task StateAndText(MessageContext context, CancellationToken cancellationToken)
                {
                    return Task.CompletedTask;
                }

                [HasPhoto]
                public Task Photo(MessageContext context)
                {
                    return Task.CompletedTask;
                }

                [CallbackDataPrefix("ticket:")]
                public Task Callback(CallbackQueryContext context)
                {
                    return Task.CompletedTask;
                }

                [UseFilter<AllowMessageFilter>]
                public Task CustomFilter(MessageContext context)
                {
                    return Task.CompletedTask;
                }

                [RequireText("hello")]
                public Task ParameterizedCustomFilter(MessageContext context)
                {
                    return Task.CompletedTask;
                }

                [RequireTelegramRole(TelegramMemberStatusSet.IsAdmin)]
                public Task AdminOnly(MessageContext context)
                {
                    return Task.CompletedTask;
                }

                [ChatMemberTransition(TelegramMemberTransition.Join)]
                public Task Joined(ChatMemberUpdatedContext context)
                {
                    return Task.CompletedTask;
                }

                [State<RegistrationStates>(nameof(RegistrationStates.Name))]
                public Task TypedState(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }

            [StateGroup("registration")]
            public sealed partial class RegistrationStates
            {
                public static partial State Name { get; }
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

            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
            public sealed class RequireTextAttribute : TelegramFilterAttribute<RequireTextFilter>
            {
                public RequireTextAttribute(string text)
                {
                    Text = text;
                }

                public string Text { get; }
            }

            public sealed class RequireTextFilter : ITelegramFilter<MessageContext, RequireTextAttribute>
            {
                public ValueTask<bool> MatchesAsync(
                    MessageContext context,
                    RequireTextAttribute attribute,
                    CancellationToken cancellationToken = default)
                {
                    return ValueTask.FromResult(true);
                }
            }
            """);

        Assert.True(
            diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.MissingRouteAttributeId) >= 8,
            "Expected route-less state, built-in filter, custom filter, role, and chat-member transition diagnostics.");
    }

    [Fact]
    public async Task Analyzer_AllowsExplicitRouteWithStateAndFilters()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class RegistrationHandlers
            {
                [Message]
                [State("registration:name")]
                [HasText]
                public Task Name(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.MissingRouteAttributeId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Analyzer_DoesNotReportRouteLessDiagnosticForErrorOnlyHandlers()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class ErrorHandlers
            {
                [Error]
                public TelegramErrorHandlingResult Handle(Exception exception)
                {
                    return TelegramErrorHandlingResult.Handled;
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.MissingRouteAttributeId);
    }

    [Fact]
    public async Task Analyzer_DoesNotReportRouteLessDiagnosticForClassBasedHandlerWithClassRoute()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            [Message]
            public sealed class NameHandler : MessageHandler
            {
                [State("registration:name")]
                [HasText]
                public Task HandleAsync(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.MissingRouteAttributeId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Generator_DoesNotInferRoutesFromStateOrFilters()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            namespace Bot;

            public sealed class RegistrationHandlers
            {
                [State("registration:name")]
                [HasText]
                public Task Name(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var generatedCompilation = RunGenerator(compilation, out var diagnostics);
        var generatedTree = generatedCompilation.SyntaxTrees
            .FirstOrDefault(tree => tree.FilePath.EndsWith("TeleFlow.Telegram.GeneratedHandlers.g.cs", StringComparison.Ordinal));

        Assert.Empty(diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Null(generatedTree);
    }

    [Theory]
    [InlineData("[Error] public Task Handle(Exception exception) => Task.CompletedTask;")]
    [InlineData("[Error] public TelegramErrorHandlingResult Handle(InvalidOperationException exception) => TelegramErrorHandlingResult.Handled;")]
    [InlineData("[Error<ArgumentException>] public TelegramErrorHandlingResult Handle(InvalidOperationException exception) => TelegramErrorHandlingResult.Handled;")]
    [InlineData("[Error] public TelegramErrorHandlingResult Handle(MessageContext first, CallbackQueryContext second) => TelegramErrorHandlingResult.Handled;")]
    [InlineData("[Error] public TelegramErrorHandlingResult Handle(Exception first, InvalidOperationException second) => TelegramErrorHandlingResult.Handled;")]
    [InlineData("[Error] public TelegramErrorHandlingResult Handle(Exception exception, CancellationToken first, CancellationToken second) => TelegramErrorHandlingResult.Handled;")]
    [InlineData("[Error] public static TelegramErrorHandlingResult Handle(Exception exception) => TelegramErrorHandlingResult.Handled;")]
    public async Task Analyzer_ReportsInvalidErrorHandlers(string methodSource)
    {
        var source = $$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class ErrorHandlers
            {
                {{methodSource}}
            }
            """;

        var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidErrorHandlerId);
    }

    [Fact]
    public async Task Analyzer_AllowsErrorOnlyTelegramModule()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            [TelegramModule("errors")]
            public sealed class ErrorModule
            {
                [Error]
                public TelegramErrorHandlingResult Handle(Exception exception)
                {
                    return TelegramErrorHandlingResult.Handled;
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidTelegramModuleId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidErrorHandlerId);
    }

    [Fact]
    public async Task Analyzer_ReportsInheritedErrorHandlerForModuleCandidateWithoutOverride()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public abstract class BaseErrors
            {
                [Error]
                public virtual TelegramErrorHandlingResult Handle(Exception exception)
                {
                    return TelegramErrorHandlingResult.Handled;
                }
            }

            [TelegramModule("errors")]
            public sealed class ErrorModule : BaseErrors
            {
            }
            """);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InheritedHandlerMethodId);
    }

    [Fact]
    public async Task Analyzer_AllowsValidClassBasedHandlers()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            [Command("start")]
            public sealed class StartHandler : MessageHandler
            {
                public Task HandleAsync(MessageContext context) => Task.CompletedTask;
            }

            [Callback<DeletePayload>]
            public sealed class DeleteHandler : CallbackHandler<DeletePayload>
            {
                public Task HandleAsync(CallbackQueryContext context, DeletePayload payload) => Task.CompletedTask;
            }

            [ChatMemberUpdated]
            public sealed class JoinHandler : ChatMemberUpdateHandler
            {
                public Task HandleAsync(ChatMemberUpdatedContext context) => Task.CompletedTask;
            }

            public sealed record DeletePayload(long Id);
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidClassBasedHandlerId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Analyzer_ReportsInvalidClassBasedHandlers()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            [Command("missing")]
            public sealed class MissingHandleHandler : MessageHandler
            {
            }

            [Command("multiple")]
            public sealed class MultipleHandleHandler : MessageHandler
            {
                public Task HandleAsync(MessageContext context) => Task.CompletedTask;
                public Task HandleAsync(MessageContext context, HandlerProbe probe) => Task.CompletedTask;
            }

            [Callback]
            public sealed class MessageBaseWithCallbackRouteHandler : MessageHandler
            {
                public Task HandleAsync(CallbackQueryContext context) => Task.CompletedTask;
            }

            [Callback<OtherPayload>]
            public sealed class MismatchedCallbackHandler : CallbackHandler<DeletePayload>
            {
                public Task HandleAsync(CallbackQueryContext context, OtherPayload payload) => Task.CompletedTask;
            }

            [Callback<DeletePayload>]
            public sealed class RawCallbackBaseWithTypedRouteHandler : CallbackHandler
            {
                public Task HandleAsync(CallbackQueryContext context, DeletePayload payload) => Task.CompletedTask;
            }

            public sealed class NonHandleRouteHandler : MessageHandler
            {
                public Task HandleAsync(MessageContext context) => Task.CompletedTask;

                [Command("other")]
                public Task Other(MessageContext context) => Task.CompletedTask;
            }

            [Command("ordinary")]
            public sealed class OrdinaryClassLevelRouteHandler
            {
                public Task HandleAsync(MessageContext context) => Task.CompletedTask;
            }

            public sealed class HandlerProbe { }
            public sealed record DeletePayload(long Id);
            public sealed record OtherPayload(long Id);
            """);

        Assert.True(
            diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidClassBasedHandlerId) >= 7,
            "Expected class-based handler diagnostics.");
    }

    [Fact]
    public async Task Analyzer_ReportsInvalidCommandAndDuplicateCommand()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class FirstHandler
            {
                [Command("/bad")]
                public Task Bad(MessageContext context) => Task.CompletedTask;

                [Command("start")]
                public Task Start(MessageContext context) => Task.CompletedTask;
            }

            public sealed class SecondHandler
            {
                [Command("start")]
                public Task Start(MessageContext context) => Task.CompletedTask;
            }
            """);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidCommandId);
        Assert.Equal(2, diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.DuplicateCommandId));
    }

    [Fact]
    public async Task Analyzer_ReportsInvalidCommandPrefixesAndPrefixedCommandTemplates()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            #nullable enable

            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class RouteHandler
            {
                [Command("start", Prefixes = new[] { "" })]
                public Task InvalidCommandPrefix(MessageContext context) => Task.CompletedTask;

                [CommandTemplate("/ban {id:int}")]
                public Task SlashPrefixedTemplate(MessageContext context, int id) => Task.CompletedTask;

                [CommandTemplate("!ban {id:int}", Prefixes = new[] { "!" })]
                public Task CustomPrefixedTemplate(MessageContext context, int id) => Task.CompletedTask;

                [CommandRegex(@"^ban (?<id>\d+)$", Prefixes = new[] { "" })]
                public Task InvalidRegexPrefix(MessageContext context, int id) => Task.CompletedTask;
            }
            """);

        Assert.Equal(2, diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidCommandPrefixId));
        Assert.True(
            diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidRouteTemplateId) >= 2,
            "Expected route template diagnostics for slash and custom-prefixed command templates.");
    }

    [Fact]
    public async Task Analyzer_ReportsTextAttributeOnCallback()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class CallbackHandler
            {
                [Callback]
                [Text("hello")]
                public Task Handle(CallbackQueryContext context) => Task.CompletedTask;
            }
            """);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.TextOnCallbackId);
    }

    [Fact]
    public async Task Analyzer_ReportsInvalidAutoAnswerCallbackUsage()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class Handlers
            {
                [Message]
                [AutoAnswerCallback("Done")]
                public Task Message(MessageContext context) => Task.CompletedTask;

                [AutoAnswerCallback]
                public Task NoRoute(CallbackQueryContext context) => Task.CompletedTask;
            }
            """);

        Assert.Equal(
            2,
            diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidAutoAnswerCallbackId));
    }

    [Fact]
    public async Task Analyzer_ReportsInvalidTypedCallbackRoutes()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            [CallbackData("del")]
            public sealed record DeletePayload(int Id);

            public interface IInvalidPayload { }

            public sealed class CallbackHandler
            {
                [Callback<DeletePayload>]
                public Task MissingPayload(CallbackQueryContext context) => Task.CompletedTask;

                [Callback<DeletePayload>]
                public Task MultiplePayloads(CallbackQueryContext context, DeletePayload first, DeletePayload second) => Task.CompletedTask;

                [Callback]
                public Task RawWithPayload(CallbackQueryContext context, DeletePayload payload) => Task.CompletedTask;

                [Callback<IInvalidPayload>]
                public Task InvalidPayloadType(CallbackQueryContext context, IInvalidPayload payload) => Task.CompletedTask;
            }
            """);

        Assert.True(
            diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidTypedCallbackId) >= 4,
            "Expected typed callback diagnostics for missing, multiple, raw payload, and invalid payload type cases.");
    }

    [Fact]
    public async Task Analyzer_ReportsInvalidCallbackDataAndDuplicatePrefixes()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using TeleFlow.Annotations;

            [CallbackData("dup")]
            public sealed record FirstPayload(string Name, decimal Amount);

            [CallbackData("dup")]
            public sealed record SecondPayload(int Id);

            [CallbackData("this_prefix_is_definitely_too_long_for_telegram_callback_data_limit")]
            public sealed record LongPrefixPayload(int Id);
            """);

        Assert.True(
            diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidCallbackDataId) >= 2,
            "Expected callback data diagnostics for unsupported field type and oversized prefix.");
        Assert.Equal(2, diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.DuplicateCallbackDataPrefixId));
    }

    [Fact]
    public async Task Analyzer_AllowsSupportedCallbackDataFieldTypes()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using TeleFlow.Annotations;

            public enum CallbackMode
            {
                View,
                Edit
            }

            [CallbackData("supported")]
            public sealed record SupportedPayload(
                string Name,
                int Page,
                long EntityId,
                bool Confirmed,
                CallbackMode Mode);
            """);

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidCallbackDataId);
    }

    [Fact]
    public async Task Analyzer_ReportsInvalidTemplatesRegexesAndRouteValues()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class RouteHandler
            {
                [TextTemplate("order {id:guid}")]
                public Task UnsupportedConstraint(MessageContext context, string id) => Task.CompletedTask;

                [TextTemplate("order {id:int} {id:int}")]
                public Task DuplicatePlaceholder(MessageContext context, int id) => Task.CompletedTask;

                [TextTemplate("order {id:int}")]
                public Task MissingParameter(MessageContext context) => Task.CompletedTask;

                [TextTemplate("order {id:int}")]
                public Task UnsupportedParameter(MessageContext context, decimal id) => Task.CompletedTask;

                [TextTemplate("optional {id:int?}")]
                public Task OptionalWithNonNullableParameter(MessageContext context, int id) => Task.CompletedTask;

                [TextTemplate("required {id:int}")]
                public Task RequiredWithNullableParameter(MessageContext context, int? id) => Task.CompletedTask;

                [TextTemplate("optional-string {name?}")]
                public Task OptionalStringWithNonNullableParameter(MessageContext context, string name) => Task.CompletedTask;

                [TextTemplate("bad {id:guid?}")]
                public Task UnsupportedOptionalConstraint(MessageContext context, int? id) => Task.CompletedTask;

                [TextTemplate("bad {id?:long}")]
                public Task NonCanonicalOptionalPlaceholder(MessageContext context, long? id) => Task.CompletedTask;

                [TextRegex("(")]
                public Task InvalidRegex(MessageContext context) => Task.CompletedTask;

                [TextRegex(@"^order (?<orderId>\d+)$")]
                public Task MissingRegexParameter(MessageContext context) => Task.CompletedTask;
            }
            """);

        Assert.True(
            diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidRouteTemplateId) >= 2,
            "Expected template diagnostics for unsupported constraint and duplicate placeholders.");
        Assert.True(
            diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidRouteValueId) >= 6,
            "Expected route-value diagnostics for missing and unsupported parameters.");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidRouteRegexId);
    }

    [Fact]
    public async Task Analyzer_ReportsInvalidBuiltInFilters()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class CallbackHandler
            {
                [Callback]
                [HasText]
                public Task Callback(CallbackQueryContext context) => Task.CompletedTask;
            }

            public sealed class MessageHandler
            {
                [Message]
                [HasCallbackData]
                public Task CallbackFilterOnMessage(MessageContext context) => Task.CompletedTask;

                [Message]
                [ChatType]
                public Task EmptyChatTypes(MessageContext context) => Task.CompletedTask;

                [Message]
                [FromUser(0)]
                public Task InvalidUser(MessageContext context) => Task.CompletedTask;

                [Message]
                [ChatType((TelegramChatType)999)]
                public Task InvalidChatType(MessageContext context) => Task.CompletedTask;

                [Message]
                [ChatId(0)]
                public Task InvalidChatId(MessageContext context) => Task.CompletedTask;

                [Message]
                [ChatUsername(" ")]
                public Task InvalidChatUsername(MessageContext context) => Task.CompletedTask;

                [Message]
                [MessageThreadId(0)]
                public Task InvalidThreadId(MessageContext context) => Task.CompletedTask;

                [Callback]
                [CallbackDataPrefix("")]
                public Task EmptyCallbackPrefix(CallbackQueryContext context) => Task.CompletedTask;
            }

            public sealed class ChatMemberHandler
            {
                [ChatMemberUpdated]
                [MessageThreadId(42)]
                public Task ThreadOnChatMember(ChatMemberUpdatedContext context) => Task.CompletedTask;

                [ChatMemberUpdated]
                [HasMessageThread]
                public Task HasThreadOnChatMember(ChatMemberUpdatedContext context) => Task.CompletedTask;
            }
            """);

        AssertInvalidFilterDiagnostic(diagnostics, "Message filters cannot be used on callback handlers.");
        AssertInvalidFilterDiagnostic(diagnostics, "Callback filters cannot be used on message handlers.");
        AssertInvalidFilterDiagnostic(diagnostics, "ChatTypeAttribute must specify at least one known Telegram chat type.");
        AssertInvalidFilterDiagnostic(diagnostics, "FromUserAttribute must specify at least one positive Telegram user id.");
        AssertInvalidFilterDiagnostic(diagnostics, "ChatIdAttribute must specify at least one non-zero Telegram chat id.");
        AssertInvalidFilterDiagnostic(diagnostics, "ChatUsernameAttribute must specify at least one non-empty Telegram chat username.");
        AssertInvalidFilterDiagnostic(diagnostics, "MessageThreadIdAttribute must specify at least one positive Telegram message thread id.");
        AssertInvalidFilterDiagnostic(diagnostics, "CallbackDataPrefixAttribute must specify a non-empty callback data prefix.");
        Assert.True(
            diagnostics.Count(
                diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidFilterId &&
                              diagnostic.GetMessage().Contains(
                                  "Message thread filters cannot be used on chat member update handlers.",
                                  StringComparison.Ordinal)) >= 2,
            "Expected diagnostics for MessageThreadId and HasMessageThread on chat-member handlers.");
    }

    [Fact]
    public async Task Analyzer_AllowsChatAndTopicFiltersOnSupportedRoutes()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class CallbackHandler
            {
                [Callback]
                [ChatType(TelegramChatType.Private)]
                [ChatId(100)]
                [ChatUsername("@admin")]
                [MessageThreadId(42)]
                [HasMessageThread]
                public Task Handle(CallbackQueryContext context) => Task.CompletedTask;
            }

            public sealed class ChatMemberHandler
            {
                [ChatMemberUpdated]
                [ChatId(-1001234567890)]
                [ChatUsername("group")]
                public Task Handle(ChatMemberUpdatedContext context) => Task.CompletedTask;
            }
            """);

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidFilterId);
    }

    [Fact]
    public async Task Analyzer_ReportsInvalidChatMemberRoutesAndTransitions()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class ChatMemberHandlers
            {
                [ChatMemberUpdated]
                public Task WrongContext(MessageContext context) => Task.CompletedTask;

                [ChatMemberUpdated]
                [Message]
                public Task MixedRoutes(ChatMemberUpdatedContext context) => Task.CompletedTask;

                [Message]
                [ChatMemberTransition(TelegramMemberTransition.Join)]
                public Task TransitionOnMessage(MessageContext context) => Task.CompletedTask;

                [ChatMemberUpdated]
                [HasText]
                public Task BuiltInFilterOnChatMember(ChatMemberUpdatedContext context) => Task.CompletedTask;

                [ChatMemberUpdated]
                [ChatMemberTransition((TelegramMemberTransition)999)]
                public Task InvalidTransition(ChatMemberUpdatedContext context) => Task.CompletedTask;

                [ChatMemberUpdated]
                [ChatMemberChanged((TelegramMemberStatusSet)999, TelegramMemberStatusSet.IsMember)]
                public Task InvalidStatusSet(ChatMemberUpdatedContext context) => Task.CompletedTask;
            }
            """);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidContextParameterId);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.MultipleRouteAttributesId);
        Assert.True(
            diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidFilterId) >= 4,
            "Expected filter diagnostics for transition placement, built-in filter placement, invalid transition, and invalid status set.");
    }

    [Fact]
    public async Task Analyzer_ReportsInvalidTelegramRoleRequirement()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class Handlers
            {
                [Message]
                [RequireTelegramRole((TelegramMemberStatusSet)999)]
                public Task Invalid(MessageContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidFilterId);
    }

    [Fact]
    public async Task Analyzer_AllowsChatMemberCustomFilters()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class ChatMemberHandlers
            {
                [ChatMemberUpdated]
                [UseFilter<ChatMemberOnlyFilter>]
                [UseFilter<UpdateFilter>]
                public Task Handle(ChatMemberUpdatedContext context) => Task.CompletedTask;
            }

            public sealed class ChatMemberOnlyFilter : ITelegramFilter<ChatMemberUpdatedContext>
            {
                public ValueTask<bool> MatchesAsync(
                    ChatMemberUpdatedContext context,
                    CancellationToken cancellationToken = default)
                {
                    return ValueTask.FromResult(true);
                }
            }

            public sealed class UpdateFilter : ITelegramFilter<TelegramUpdateContext>
            {
                public ValueTask<bool> MatchesAsync(
                    TelegramUpdateContext context,
                    CancellationToken cancellationToken = default)
                {
                    return ValueTask.FromResult(true);
                }
            }
            """);

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidFilterId);
    }

    [Fact]
    public async Task Analyzer_AllowsTrimmedCallbackDataPrefix()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class CallbackHandler
            {
                [Callback]
                [CallbackDataPrefix(" admin: ")]
                public Task Handle(CallbackQueryContext context) => Task.CompletedTask;
            }
            """);

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidFilterId);
    }

    [Fact]
    public async Task Analyzer_ReportsInvalidCustomFilters()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class MessageHandler
            {
                [Message]
                [UseFilter<CallbackOnlyFilter>]
                public Task Incompatible(MessageContext context) => Task.CompletedTask;

                [Message]
                [UseFilter<NotAFilter>]
                public Task NotFilter(MessageContext context) => Task.CompletedTask;

                [Message]
                [UseFilter<AbstractMessageFilter>]
                public Task AbstractFilter(MessageContext context) => Task.CompletedTask;
            }

            public sealed class CallbackHandler
            {
                [Callback]
                [UseFilter<MessageOnlyFilter>]
                public Task Incompatible(CallbackQueryContext context) => Task.CompletedTask;
            }

            public sealed class CallbackOnlyFilter : ITelegramFilter<CallbackQueryContext>
            {
                public ValueTask<bool> MatchesAsync(
                    CallbackQueryContext context,
                    CancellationToken cancellationToken = default)
                {
                    return ValueTask.FromResult(true);
                }
            }

            public sealed class MessageOnlyFilter : ITelegramFilter<MessageContext>
            {
                public ValueTask<bool> MatchesAsync(
                    MessageContext context,
                    CancellationToken cancellationToken = default)
                {
                    return ValueTask.FromResult(true);
                }
            }

            public abstract class AbstractMessageFilter : ITelegramFilter<MessageContext>
            {
                public ValueTask<bool> MatchesAsync(
                    MessageContext context,
                    CancellationToken cancellationToken = default)
                {
                    return ValueTask.FromResult(true);
                }
            }

            public sealed class NotAFilter
            {
            }
            """);

        Assert.True(
            diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidFilterId) >= 4,
            "Expected filter diagnostics for incompatible, abstract, and non-filter custom filter types.");
    }

    [Fact]
    public async Task Analyzer_ReportsParameterizedCustomFilterWithoutTypedContract()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            public sealed class MessageHandler
            {
                [Message]
                [RequireText("hello")]
                public Task Handle(MessageContext context) => Task.CompletedTask;
            }

            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
            public sealed class RequireTextAttribute : TelegramFilterAttribute<RequireTextFilter>
            {
                public RequireTextAttribute(string text)
                {
                    Text = text;
                }

                public string Text { get; }
            }

            public sealed class RequireTextFilter : ITelegramFilter<MessageContext>
            {
                public ValueTask<bool> MatchesAsync(
                    MessageContext context,
                    CancellationToken cancellationToken = default)
                {
                    return ValueTask.FromResult(true);
                }
            }
            """);

        AssertInvalidFilterDiagnostic(
            diagnostics,
            "Parameterized custom Telegram filter attributes require a filter type that implements ITelegramFilter<TContext, TAttribute>.");
    }

    [Fact]
    public async Task Analyzer_ReportsInvalidTypedStateReference()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Framework.States;
            using TeleFlow.Telegram;

            [StateGroup("registration")]
            public sealed partial class RegistrationStates
            {
                public static partial State Name { get; }
            }

            public sealed class Handler
            {
                [Message]
                [State<RegistrationStates>("Missing")]
                public Task Handle(MessageContext context) => Task.CompletedTask;
            }
            """);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidTypedStateId);
    }

    [Fact]
    public async Task Analyzer_AllowsValidScene()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Framework.States;
            using TeleFlow.Telegram;

            [Scene("registration")]
            public sealed partial class RegistrationScene
            {
                public static partial State Name { get; }

                [Message]
                [SceneStep(nameof(Name))]
                public Task NameStep(MessageContext context) => Task.CompletedTask;
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidSceneId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidSceneStepId);
    }

    [Fact]
    public async Task Analyzer_ReportsInvalidScenesAndSceneSteps()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Framework.States;
            using TeleFlow.Telegram;

            [Scene("")]
            public sealed partial class EmptyPrefixScene
            {
                public static partial State Name { get; }
            }

            [Scene("non-partial")]
            public sealed class NonPartialScene
            {
                public static State Name => State.Create("non-partial:name");
            }

            [Scene("empty")]
            public sealed partial class EmptyScene
            {
            }

            [Scene("invalid-state")]
            public sealed partial class InvalidStateScene
            {
                public static State Name => State.Create("invalid-state:name");
            }

            [Scene("duplicate")]
            public sealed partial class DuplicateStateScene
            {
                [StateValue("same")]
                public static partial State First { get; }

                [StateValue("same")]
                public static partial State Second { get; }
            }

            public sealed class OutsideSceneHandler
            {
                [Message]
                [SceneStep("Name")]
                public Task Handle(MessageContext context) => Task.CompletedTask;
            }

            [Scene("no-route")]
            public sealed partial class NoRouteScene
            {
                public static partial State Name { get; }

                [SceneStep(nameof(Name))]
                public Task Handle(MessageContext context) => Task.CompletedTask;
            }

            [Scene("missing")]
            public sealed partial class MissingStateScene
            {
                public static partial State Name { get; }

                [Message]
                [SceneStep("Unknown")]
                public Task Handle(MessageContext context) => Task.CompletedTask;
            }

            [Scene("mixed")]
            public sealed partial class MixedStateScene
            {
                public static partial State Name { get; }

                [Message]
                [SceneStep(nameof(Name))]
                [State("legacy")]
                public Task Handle(MessageContext context) => Task.CompletedTask;
            }
            """);

        Assert.True(
            diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidSceneId) >= 5,
            "Expected scene declaration diagnostics.");
        Assert.True(
            diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidSceneStepId) >= 4,
            "Expected scene step diagnostics.");
    }

    [Fact]
    public async Task Analyzer_ReportsSceneStepReferencesToNonPublicOrInstanceStateProperties()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Framework.States;
            using TeleFlow.Telegram;

            [Scene("private-state")]
            public sealed partial class PrivateStateScene
            {
                public static partial State Name { get; }

                private static State Hidden => State.Create("private-state:hidden");

                [Message]
                [SceneStep(nameof(Hidden))]
                public Task Handle(MessageContext context) => Task.CompletedTask;
            }

            [Scene("instance-state")]
            public sealed partial class InstanceStateScene
            {
                public static partial State Name { get; }

                public State Instance => State.Create("instance-state:instance");

                [Message]
                [SceneStep(nameof(Instance))]
                public Task Handle(MessageContext context) => Task.CompletedTask;
            }
            """);

        Assert.Equal(
            2,
            diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidSceneStepId));
    }

    [Fact]
    public async Task Analyzer_ReportsInvalidTelegramModules()
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            [TelegramModule("abstract")]
            public abstract class AbstractModule
            {
                [Message]
                public Task Handle(MessageContext context) => Task.CompletedTask;
            }

            [TelegramModule("empty")]
            public sealed class EmptyModule
            {
            }

            [TelegramModule("static")]
            public static class StaticModule
            {
            }
            """);

        Assert.True(
            diagnostics.Count(diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidTelegramModuleId) >= 3,
            "Expected module diagnostics for abstract, empty, and static module declarations.");
    }

    private static async Task<IReadOnlyList<Diagnostic>> GetAnalyzerDiagnosticsAsync(string source)
    {
        var compilation = CreateCompilation(source);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new TelegramHandlerAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static void AssertInvalidFilterDiagnostic(
        IReadOnlyList<Diagnostic> diagnostics,
        string message)
    {
        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Id == TelegramHandlerAnalyzer.InvalidFilterId &&
                          diagnostic.GetMessage().Contains(message, StringComparison.Ordinal));
    }

    private static CSharpCompilation CreateCompilation(string source)
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
        references.Add(MetadataReference.CreateFromFile(typeof(GeneratedErrorRuntimeProbe).Assembly.Location));

        return CSharpCompilation.Create(
            "GeneratorTests",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static Compilation RunGenerator(
        CSharpCompilation compilation,
        out ImmutableArray<Diagnostic> diagnostics)
    {
        var driver = CSharpGeneratorDriver.Create(new TelegramHandlerSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out diagnostics);

        return generatedCompilation;
    }

    private static Assembly EmitAndLoad(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Diagnostics
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(static diagnostic => diagnostic.ToString())));

        return Assembly.Load(stream.ToArray());
    }

    private static async Task DispatchAsync(
        ServiceProvider serviceProvider,
        Update update,
        CancellationToken cancellationToken)
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

    private static Update CreateMessageUpdate(string text)
    {
        return new Update
        {
            UpdateId = 1,
            Message = new Message
            {
                MessageId = 10,
                Date = 0,
                Chat = new Chat { Id = 100, Type = "private" },
                From = new User { Id = 5, IsBot = false, FirstName = "User" },
                Text = text
            }
        };
    }

    private static Update CreateCallbackUpdate(string? data)
    {
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
                Data = data
            }
        };
    }
}

public static class GeneratedErrorRuntimeProbe
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, List<string>> Events = [];

    public static void Clear(string runId)
    {
        lock (Sync)
        {
            Events.Remove(runId);
        }
    }

    public static void Record(string runId, string value)
    {
        lock (Sync)
        {
            if (!Events.TryGetValue(runId, out var values))
            {
                values = [];
                Events.Add(runId, values);
            }

            values.Add(value);
        }
    }

    public static IReadOnlyList<string> GetEvents(string runId)
    {
        lock (Sync)
        {
            return Events.TryGetValue(runId, out var values)
                ? values.ToArray()
                : [];
        }
    }
}
