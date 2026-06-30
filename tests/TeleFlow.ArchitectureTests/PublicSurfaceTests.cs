using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TeleFlow.Core.Application;
using TeleFlow.Storage.Memory;

namespace TeleFlow.ArchitectureTests;

public sealed class PublicSurfaceTests
{
    private static readonly string[] Phase1aClientTypeNames =
    [
        "TeleFlow.Telegram.ITelegramTransport",
        "TeleFlow.Telegram.TelegramTransportRequest",
        "TeleFlow.Telegram.TelegramTransportResponse",
        "TeleFlow.Telegram.TelegramTransportContent",
        "TeleFlow.Telegram.TelegramJsonTransportContent",
        "TeleFlow.Telegram.TelegramMultipartTransportContent",
        "TeleFlow.Telegram.TelegramMultipartField",
        "TeleFlow.Telegram.TelegramMultipartFile",
        "TeleFlow.Telegram.TelegramBotDefaults",
        "TeleFlow.Telegram.TelegramParseMode",
        "TeleFlow.Telegram.TelegramJsonOptions",
        "TeleFlow.Telegram.TelegramException",
        "TeleFlow.Telegram.TelegramRequestException",
        "TeleFlow.Telegram.TelegramApiException",
        "TeleFlow.Telegram.TelegramBadRequestException",
        "TeleFlow.Telegram.TelegramUnauthorizedException",
        "TeleFlow.Telegram.TelegramForbiddenException",
        "TeleFlow.Telegram.TelegramNotFoundException",
        "TeleFlow.Telegram.TelegramConflictException",
        "TeleFlow.Telegram.TelegramEntityTooLargeException",
        "TeleFlow.Telegram.TelegramRetryAfterException",
        "TeleFlow.Telegram.TelegramMigrateToChatException",
        "TeleFlow.Telegram.TelegramServerException",
        "TeleFlow.Telegram.TelegramNetworkException",
        "TeleFlow.Telegram.TelegramDecodeException"
    ];

    private static readonly string[] Phase1bClientTypeNames =
    [
        "TeleFlow.Telegram.ITelegramClient",
        "TeleFlow.Telegram.ITelegramRequestExecutor",
        "TeleFlow.Telegram.ITelegramRequest`1",
        "TeleFlow.Telegram.ITelegramResponse",
        "TeleFlow.Telegram.TelegramClientOptions",
        "TeleFlow.Telegram.HttpClientTelegramTransport",
        "TeleFlow.Telegram.TelegramDeepLinks",
        "TeleFlow.Telegram.IDeepLinkPayloadSerializer",
        "TeleFlow.Telegram.Base64UrlJsonDeepLinkPayloadSerializer",
        "TeleFlow.Telegram.TelegramClientServiceCollectionExtensions"
    ];

    private static readonly string[] Phase1cClientTypeNames =
    [
        "TeleFlow.Telegram.TelegramUpdateType",
        "TeleFlow.Telegram.TelegramClientGetMeExtensions",
        "TeleFlow.Telegram.TelegramClientSendMessageExtensions",
        "TeleFlow.Telegram.TelegramClientSendPhotoExtensions",
        "TeleFlow.Telegram.TelegramClientAnswerCallbackQueryExtensions"
    ];

    private static readonly string[] Phase3aFrameworkTypeNames =
    [
        "TeleFlow.Telegram.TelegramUpdateContext",
        "TeleFlow.Telegram.MessageContext",
        "TeleFlow.Telegram.CallbackQueryContext",
        "TeleFlow.Telegram.ChatMemberUpdatedContext",
        "TeleFlow.Telegram.MessageHandler",
        "TeleFlow.Telegram.CallbackHandler",
        "TeleFlow.Telegram.CallbackHandler`1",
        "TeleFlow.Telegram.ChatMemberUpdateHandler",
        "TeleFlow.Telegram.ITelegramFilter`1",
        "TeleFlow.Telegram.TelegramUpdatePayload",
        "TeleFlow.Telegram.TelegramServiceCollectionExtensions",
        "TeleFlow.Telegram.TelegramBotOptions",
        "TeleFlow.Telegram.TelegramErrorContext",
        "TeleFlow.Telegram.TelegramErrorHandlingResult",
        "TeleFlow.Telegram.TelegramGeneratedHandlersAttribute",
        "TeleFlow.Telegram.ITelegramGeneratedHandlerRegistrar",
        "TeleFlow.Telegram.ITelegramGeneratedHandlerRegistry",
        "TeleFlow.Telegram.TelegramGeneratedHandlerDescriptor",
        "TeleFlow.Telegram.TelegramGeneratedErrorHandlerDescriptor",
        "TeleFlow.Telegram.TelegramGeneratedErrorHandlerInvoker",
        "TeleFlow.Telegram.TelegramGeneratedErrorHandlerParameterDescriptor",
        "TeleFlow.Telegram.TelegramGeneratedErrorHandlerParameterKind",
        "TeleFlow.Telegram.TelegramGeneratedFilterDescriptor",
        "TeleFlow.Telegram.TelegramGeneratedHandlerKind"
    ];

    private static readonly string[] Phase4aRawLongPollingTypeNames =
    [
        "TeleFlow.Telegram.ITelegramLongPollingClient",
        "TeleFlow.Telegram.TelegramLongPollingClient",
        "TeleFlow.Telegram.TelegramRawLongPollingOptions",
        "TeleFlow.Telegram.TelegramRawLongPollingBackoffOptions",
        "TeleFlow.Telegram.TelegramPolledUpdate",
        "TeleFlow.Telegram.TelegramLongPollingClientServiceCollectionExtensions"
    ];

    private static readonly string[] Phase5FrameworkLongPollingTypeNames =
    [
        "TeleFlow.Telegram.TelegramLongPollingServiceCollectionExtensions",
        "TeleFlow.Telegram.TelegramLongPollingOptions",
        "TeleFlow.Telegram.TelegramAllowedUpdates",
        "TeleFlow.Telegram.TelegramBackoffOptions"
    ];

    private static readonly string[] Phase6RawWebhookTypeNames =
    [
        "TeleFlow.Telegram.Webhooks.TelegramRawWebhookOptions",
        "TeleFlow.Telegram.Webhooks.TelegramRawWebhookHandler",
        "TeleFlow.Telegram.Webhooks.TelegramRawWebhookEndpointRouteBuilderExtensions"
    ];

    private static readonly string[] Phase7FrameworkWebhookTypeNames =
    [
        "TeleFlow.Telegram.Webhooks.TelegramWebhookOptions",
        "TeleFlow.Telegram.Webhooks.TelegramWebhookServiceCollectionExtensions",
        "TeleFlow.Telegram.Webhooks.TelegramWebhookEndpointRouteBuilderExtensions"
    ];

    private static readonly string[] MemoryStorageTypeNames =
    [
        "TeleFlow.Storage.Memory.MemoryStateDataStore",
        "TeleFlow.Storage.Memory.MemoryStateHistoryStore",
        "TeleFlow.Storage.Memory.MemoryStateServiceCollectionExtensions",
        "TeleFlow.Storage.Memory.MemoryStateStore"
    ];

    private static readonly string[] ClientOnlyTeleFlowReferenceFileNames =
    [
        "TeleFlow.Telegram.Client.dll",
        "TeleFlow.Telegram.Schema.dll"
    ];

    private static readonly string[] FrameworkOnlyTeleFlowReferenceFileNames =
    [
        "TeleFlow.Annotations.dll",
        "TeleFlow.Core.dll",
        "TeleFlow.Telegram.Client.dll",
        "TeleFlow.Telegram.Framework.dll",
        "TeleFlow.Telegram.Schema.dll"
    ];

    private static readonly string[] RawLongPollingOnlyTeleFlowReferenceFileNames =
    [
        "TeleFlow.Telegram.Client.dll",
        "TeleFlow.Telegram.LongPolling.dll",
        "TeleFlow.Telegram.Schema.dll"
    ];

    private static readonly string[] FrameworkLongPollingOnlyTeleFlowReferenceFileNames =
    [
        "TeleFlow.Annotations.dll",
        "TeleFlow.Core.dll",
        "TeleFlow.Telegram.Client.dll",
        "TeleFlow.Telegram.Framework.LongPolling.dll",
        "TeleFlow.Telegram.Framework.dll",
        "TeleFlow.Telegram.LongPolling.dll",
        "TeleFlow.Telegram.Schema.dll"
    ];

    private static readonly string[] RawWebhooksOnlyTeleFlowReferenceFileNames =
    [
        "TeleFlow.Telegram.Client.dll",
        "TeleFlow.Telegram.Schema.dll",
        "TeleFlow.Telegram.Webhooks.dll"
    ];

    private static readonly string[] FrameworkWebhooksOnlyTeleFlowReferenceFileNames =
    [
        "TeleFlow.Annotations.dll",
        "TeleFlow.Core.dll",
        "TeleFlow.Telegram.Client.dll",
        "TeleFlow.Telegram.Framework.Webhooks.dll",
        "TeleFlow.Telegram.Framework.dll",
        "TeleFlow.Telegram.Schema.dll",
        "TeleFlow.Telegram.Webhooks.dll"
    ];

    private static readonly string[] TelegramDefaultPackageTeleFlowReferenceFileNames =
    [
        "TeleFlow.Telegram.Client.dll",
        "TeleFlow.Telegram.Schema.dll",
        "TeleFlow.Telegram.dll"
    ];

    [Fact]
    public void Core_DoesNotReferenceTelegramAssemblies()
    {
        var referencedAssemblies = typeof(TeleFlowApplication).Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("TeleFlow.Telegram", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram.Schema", referencedAssemblies);
    }

    [Fact]
    public void Core_PublicSurface_UsesOnlyCoreNamespaces()
    {
        var publicTypes = typeof(TeleFlowApplication).Assembly
            .GetExportedTypes()
            .Where(static type => type.IsPublic)
            .ToArray();

        Assert.All(publicTypes, static type => Assert.StartsWith("TeleFlow.Core", type.Namespace));
    }

    [Fact]
    public void Core_PublicSurface_UsesStatesNamespaceForStateContracts()
    {
        var exportedTypeNames = typeof(TeleFlowApplication).Assembly
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .Where(static name => name is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("TeleFlow.Core.States.State", exportedTypeNames);
        Assert.Contains("TeleFlow.Core.States.UpdateState", exportedTypeNames);
        Assert.DoesNotContain(exportedTypeNames, static name =>
            name.StartsWith("TeleFlow.Core.State.", StringComparison.Ordinal));
    }

    [Fact]
    public void Core_PublicSurface_DoesNotExposeTelegramRequestExecutionContracts()
    {
        var exportedTypeNames = typeof(TeleFlowApplication).Assembly
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("TeleFlow.Core.Telegram.ITelegramRequestExecutor", exportedTypeNames);
        Assert.DoesNotContain("TeleFlow.Core.Telegram.ITelegramRequest`1", exportedTypeNames);
        Assert.DoesNotContain("TeleFlow.Core.Telegram.ITelegramResponse", exportedTypeNames);
    }

    [Fact]
    public void StorageMemory_DoesNotReferenceTelegramAssemblies()
    {
        var referencedAssemblies = typeof(MemoryStateStore).Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .ToArray();

        Assert.Contains("TeleFlow.Core", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram.Schema", referencedAssemblies);
    }

    [Fact]
    public void StorageMemory_PublicSurface_UsesOnlyStorageMemoryNamespace()
    {
        var publicTypes = typeof(MemoryStateStore).Assembly
            .GetExportedTypes()
            .Where(static type => type.IsPublic)
            .ToArray();

        Assert.All(publicTypes, static type => Assert.StartsWith("TeleFlow.Storage.Memory", type.Namespace));
    }

    [Fact]
    public void StorageMemory_PublicSurface_ExportsMemoryProviderTypes()
    {
        var exportedTypeNames = typeof(MemoryStateStore).Assembly
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            MemoryStorageTypeNames.Order(StringComparer.Ordinal),
            exportedTypeNames.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TelegramClient_DoesNotReferenceCoreOrFrameworkRuntimeAssemblies()
    {
        var referencedAssemblies = LoadProjectAssembly("TeleFlow.Telegram.Client")
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("TeleFlow.Core", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Annotations", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram.Framework", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram.Webhooks", referencedAssemblies);
    }

    [Fact]
    public void TelegramClient_PublicSurface_DoesNotExposeFrameworkRuntimeTypes()
    {
        var publicTypes = LoadProjectAssembly("TeleFlow.Telegram.Client")
            .GetExportedTypes()
            .ToArray();

        Assert.DoesNotContain(publicTypes, static type => IsFrameworkRuntimeType(type));
    }

    [Fact]
    public void TelegramClient_PublicSurface_ExportsClientContracts()
    {
        var exportedTypeNames = LoadProjectAssembly("TeleFlow.Telegram.Client")
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(Phase1aClientTypeNames, name => Assert.Contains(name, exportedTypeNames));
        Assert.All(Phase1bClientTypeNames, name => Assert.Contains(name, exportedTypeNames));
        Assert.All(Phase1cClientTypeNames, name => Assert.Contains(name, exportedTypeNames));
    }

    [Fact]
    public void Telegram_PublicSurface_DoesNotDeclareDuplicateClientContracts()
    {
        var exportedTypeNames = LoadProjectAssembly("TeleFlow.Telegram")
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(Phase1aClientTypeNames, name => Assert.DoesNotContain(name, exportedTypeNames));
        Assert.All(Phase1bClientTypeNames, name => Assert.DoesNotContain(name, exportedTypeNames));
        Assert.All(Phase1cClientTypeNames, name => Assert.DoesNotContain(name, exportedTypeNames));
    }

    [Fact]
    public void TelegramFramework_PublicSurface_ExportsFrameworkRuntimeTypes()
    {
        var exportedTypeNames = LoadProjectAssembly("TeleFlow.Telegram.Framework")
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(Phase3aFrameworkTypeNames, name => Assert.Contains(name, exportedTypeNames));
    }

    [Fact]
    public void TelegramFramework_ReflectionAssemblyRegistration_IsObsolete()
    {
        var method = typeof(TeleFlow.Telegram.TelegramServiceCollectionExtensions).GetMethod(
            nameof(TeleFlow.Telegram.TelegramServiceCollectionExtensions.AddTelegramHandlersFromAssemblyReflection),
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);

        var obsolete = method.GetCustomAttribute<ObsoleteAttribute>();

        Assert.NotNull(obsolete);
        Assert.False(obsolete.IsError);
        Assert.Equal("TLF900", obsolete.DiagnosticId);
        Assert.Contains("Reflection-based assembly handler registration is deprecated", obsolete.Message);
        Assert.Contains("IWF.TeleFlow.Generators", obsolete.Message);
        Assert.Contains("AddTelegramHandler<THandler>", obsolete.Message);
        Assert.Contains("AddTelegramModule<TModule>", obsolete.Message);
    }

    [Fact]
    public void Telegram_PublicSurface_DoesNotDeclareDuplicateFrameworkRuntimeTypes()
    {
        var exportedTypeNames = LoadProjectAssembly("TeleFlow.Telegram")
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(Phase3aFrameworkTypeNames, name => Assert.DoesNotContain(name, exportedTypeNames));
    }

    [Fact]
    public void Telegram_PublicSurface_IsEmptyClientConveniencePackage()
    {
        var exportedTypes = LoadProjectAssembly("TeleFlow.Telegram")
            .GetExportedTypes();

        Assert.Empty(exportedTypes);
    }

    [Fact]
    public void Packages_DoNotExposeSpeculativeAssemblyMarkerTypes()
    {
        var removedMarkerTypes = new[]
        {
            ("TeleFlow.Generators", "TeleFlow.Generators.GeneratorsMarker"),
            ("TeleFlow.Storage.Memory", "TeleFlow.Storage.Memory.MemoryStorageMarker"),
            ("TeleFlow.Telegram.Framework", "TeleFlow.Telegram.TelegramIntegrationMarker"),
            ("TeleFlow.Telegram.Schema", "TeleFlow.Telegram.Schema.TelegramSchemaMarker")
        };

        foreach (var (assemblyName, typeName) in removedMarkerTypes)
        {
            var exportedTypeNames = LoadProjectAssembly(assemblyName)
                .GetExportedTypes()
                .Select(static type => type.FullName)
                .ToHashSet(StringComparer.Ordinal);

            Assert.DoesNotContain(typeName, exportedTypeNames);
        }
    }

    [Fact]
    public void Packages_DoNotKeepStaleInternalsVisibleToGrants()
    {
        var removedFriendGrants = new[]
        {
            ("TeleFlow.Telegram.Client", "TeleFlow.Telegram"),
            ("TeleFlow.Telegram.Webhooks", "TeleFlow.ArchitectureTests")
        };

        foreach (var (assemblyName, friendAssemblyName) in removedFriendGrants)
        {
            var friendAssemblyNames = GetInternalsVisibleToAssemblyNames(LoadProjectAssembly(assemblyName));

            Assert.DoesNotContain(friendAssemblyName, friendAssemblyNames);
        }
    }

    [Fact]
    public void TelegramFramework_PublicSurface_DoesNotDeclareFrameworkLongPollingTypes()
    {
        var exportedTypeNames = LoadProjectAssembly("TeleFlow.Telegram.Framework")
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(Phase5FrameworkLongPollingTypeNames, name => Assert.DoesNotContain(name, exportedTypeNames));
    }

    [Fact]
    public void TelegramLongPolling_DoesNotReferenceCoreOrFrameworkRuntimeAssemblies()
    {
        var referencedAssemblies = LoadProjectAssembly("TeleFlow.Telegram.LongPolling")
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("TeleFlow.Core", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Annotations", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram.Framework", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram.Webhooks", referencedAssemblies);
    }

    [Fact]
    public void TelegramLongPolling_PublicSurface_ExportsRawPollingTypes()
    {
        var exportedTypeNames = LoadProjectAssembly("TeleFlow.Telegram.LongPolling")
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(Phase4aRawLongPollingTypeNames, name => Assert.Contains(name, exportedTypeNames));
    }

    [Fact]
    public void TelegramLongPolling_PublicSurface_DoesNotExposeFrameworkRuntimeTypes()
    {
        var publicTypes = LoadProjectAssembly("TeleFlow.Telegram.LongPolling")
            .GetExportedTypes()
            .ToArray();

        Assert.DoesNotContain(publicTypes, static type =>
            IsFrameworkRuntimeType(type) ||
            string.Equals(type.FullName, "TeleFlow.Telegram.TelegramAllowedUpdates", StringComparison.Ordinal) ||
            type.FullName?.Contains("Handler", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void TelegramFrameworkLongPolling_DoesNotReferenceWebhooksOrOldTelegramAssemblies()
    {
        var referencedAssemblies = LoadProjectAssembly("TeleFlow.Telegram.Framework.LongPolling")
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .ToArray();

        Assert.Contains("TeleFlow.Core", referencedAssemblies);
        Assert.Contains("TeleFlow.Telegram.Framework", referencedAssemblies);
        Assert.Contains("TeleFlow.Telegram.LongPolling", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram.Webhooks", referencedAssemblies);
    }

    [Fact]
    public void TelegramFrameworkLongPolling_PublicSurface_ExportsFrameworkPollingTypes()
    {
        var exportedTypeNames = LoadProjectAssembly("TeleFlow.Telegram.Framework.LongPolling")
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(Phase5FrameworkLongPollingTypeNames, name => Assert.Contains(name, exportedTypeNames));
    }

    [Fact]
    public void TelegramFrameworkLongPolling_PublicSurface_DoesNotExportRawPollingTypes()
    {
        var exportedTypeNames = LoadProjectAssembly("TeleFlow.Telegram.Framework.LongPolling")
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(Phase4aRawLongPollingTypeNames, name => Assert.DoesNotContain(name, exportedTypeNames));
    }

    [Fact]
    public void TelegramWebhooks_DoesNotReferenceCoreFrameworkOrOldTelegramAssemblies()
    {
        var referencedAssemblies = LoadProjectAssembly("TeleFlow.Telegram.Webhooks")
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .ToArray();

        Assert.Contains("TeleFlow.Telegram.Client", referencedAssemblies);
        Assert.Contains("TeleFlow.Telegram.Schema", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Core", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Annotations", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram.Framework", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram.Framework.LongPolling", referencedAssemblies);
    }

    [Fact]
    public void TelegramWebhooks_PublicSurface_ExportsRawWebhookTypes()
    {
        var exportedTypeNames = LoadProjectAssembly("TeleFlow.Telegram.Webhooks")
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(Phase6RawWebhookTypeNames, name => Assert.Contains(name, exportedTypeNames));
    }

    [Fact]
    public void TelegramWebhooks_PublicSurface_DoesNotExposeFrameworkRuntimeTypes()
    {
        var publicTypes = LoadProjectAssembly("TeleFlow.Telegram.Webhooks")
            .GetExportedTypes()
            .ToArray();

        Assert.DoesNotContain(publicTypes, static type =>
            (!Phase6RawWebhookTypeNames.Contains(type.FullName, StringComparer.Ordinal) &&
             IsFrameworkRuntimeType(type)) ||
            type.FullName?.Contains("HandlerDescriptor", StringComparison.Ordinal) == true ||
            type.FullName?.Contains("TelegramWebhookOptions", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void TelegramFrameworkWebhooks_DoesNotReferenceLongPollingOrOldTelegramAssemblies()
    {
        var referencedAssemblies = LoadProjectAssembly("TeleFlow.Telegram.Framework.Webhooks")
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .ToArray();

        Assert.Contains("TeleFlow.Core", referencedAssemblies);
        Assert.Contains("TeleFlow.Telegram.Framework", referencedAssemblies);
        Assert.Contains("TeleFlow.Telegram.Webhooks", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram.LongPolling", referencedAssemblies);
        Assert.DoesNotContain("TeleFlow.Telegram.Framework.LongPolling", referencedAssemblies);
    }

    [Fact]
    public void TelegramFrameworkWebhooks_PublicSurface_ExportsFrameworkWebhookTypes()
    {
        var exportedTypeNames = LoadProjectAssembly("TeleFlow.Telegram.Framework.Webhooks")
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(Phase7FrameworkWebhookTypeNames, name => Assert.Contains(name, exportedTypeNames));
    }

    [Fact]
    public void TelegramFrameworkWebhooks_PublicSurface_DoesNotExportRawWebhookTypes()
    {
        var exportedTypeNames = LoadProjectAssembly("TeleFlow.Telegram.Framework.Webhooks")
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(Phase6RawWebhookTypeNames, name => Assert.DoesNotContain(name, exportedTypeNames));
    }

    [Fact]
    public void TelegramClient_Phase1aContracts_AreUsable()
    {
        var defaults = new global::TeleFlow.Telegram.TelegramBotDefaults
        {
            ParseMode = global::TeleFlow.Telegram.TelegramParseMode.Html,
            DisableNotification = true
        };
        var jsonOptions = global::TeleFlow.Telegram.TelegramJsonOptions.CreateDefault();
        var content = new global::TeleFlow.Telegram.TelegramJsonTransportContent("{}");
        var request = new global::TeleFlow.Telegram.TelegramTransportRequest(
            "getMe",
            new Uri("https://api.telegram.org/bot123/getMe"),
            content);
        var response = new global::TeleFlow.Telegram.TelegramTransportResponse(
            200,
            "{}",
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["Retry-After"] = ["1"]
            });
        var exception = new global::TeleFlow.Telegram.TelegramRetryAfterException(
            "retry",
            methodName: request.MethodName,
            retryAfterSeconds: 1);

        Assert.Equal(global::TeleFlow.Telegram.TelegramParseMode.Html, defaults.ParseMode);
        Assert.False(jsonOptions.SerializerOptions.PropertyNameCaseInsensitive);
        Assert.Same(content, request.Content);
        Assert.True(response.TryGetHeaderValues("retry-after", out var values));
        Assert.Equal("1", Assert.Single(values));
        Assert.Equal(TimeSpan.FromSeconds(1), exception.RetryAfter);
    }

    [Fact]
    public void TelegramClient_GeneratedSurface_CompilesWithoutOldTelegramPackage()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using TeleFlow.Telegram;
            using TeleFlow.Telegram.Schema.Abstractions;
            using TeleFlow.Telegram.Schema.Types;

            public static class ClientOnlySmoke
            {
                public static async Task RunAsync(ITelegramClient bot)
                {
                    User me = await bot.GetMeAsync(CancellationToken.None);
                    Message message = await bot.SendMessageAsync(
                        IntegerString.From(123),
                        "hello",
                        parseMode: TelegramParseMode.Html,
                        cancellationToken: CancellationToken.None);
                    TelegramUpdateType updateType = TelegramUpdateType.CallbackQuery;

                    _ = me.Id;
                    _ = message.MessageId;
                    _ = updateType.Value;
                }
            }
            """;

        var compilation = CreateClientOnlyCompilation(source);
        using var stream = new MemoryStream();

        var result = compilation.Emit(stream);

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Diagnostics
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(static diagnostic => diagnostic.ToString())));
    }

    [Fact]
    public void TelegramFramework_GeneratedHandlerSurface_CompilesWithoutOldTelegramPackage()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using TeleFlow.Annotations;
            using TeleFlow.Telegram;

            [assembly: TelegramGeneratedHandlersAttribute(typeof(TeleFlow.Generated.TelegramGeneratedHandlersRegistrar))]

            namespace TeleFlow.Generated;

            internal sealed class TelegramGeneratedHandlersRegistrar : ITelegramGeneratedHandlerRegistrar
            {
                public void Register(ITelegramGeneratedHandlerRegistry registry)
                {
                    registry.RegisterHandler(new TelegramGeneratedHandlerDescriptor(
                        handlerType: typeof(Handler),
                        methodName: nameof(Handler.Start),
                        kind: TelegramGeneratedHandlerKind.Command,
                        routeKind: TelegramGeneratedRouteKind.CommandExact,
                        routePattern: null,
                        commandPrefixes: ["/"],
                        allowSpaceAfterPrefix: true,
                        ignoreCase: true,
                        registrationOrder: 0,
                        moduleName: null,
                        command: "start",
                        callbackPayloadType: null,
                        textFilters: Array.Empty<TelegramGeneratedTextFilterDescriptor>(),
                        filters: Array.Empty<TelegramGeneratedFilterDescriptor>(),
                        chatMemberTransitions: Array.Empty<TelegramGeneratedChatMemberTransitionDescriptor>(),
                        roleRequirements: Array.Empty<TelegramGeneratedRoleRequirementDescriptor>(),
                        states: Array.Empty<string>(),
                        parameters: [new TelegramGeneratedHandlerParameterDescriptor(typeof(MessageContext), TelegramGeneratedHandlerParameterKind.Context, "context")],
                        invoker: static (_, _, _) => ValueTask.CompletedTask,
                        routeValues: Array.Empty<TelegramGeneratedRouteValueDescriptor>()));
                }
            }

            internal sealed class Handler
            {
                [Command("start")]
                public Task Start(MessageContext context) => Task.CompletedTask;
            }
            """;

        var compilation = CreateFrameworkOnlyCompilation(source);
        using var stream = new MemoryStream();

        var result = compilation.Emit(stream);

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Diagnostics
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(static diagnostic => diagnostic.ToString())));
    }

    [Fact]
    public void TelegramLongPolling_RawSurface_CompilesWithoutFrameworkPackage()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using TeleFlow.Telegram;
            using TeleFlow.Telegram.Schema.Types;

            public static class RawLongPollingOnlySmoke
            {
                public static Task RunAsync(ITelegramLongPollingClient polling, CancellationToken cancellationToken)
                {
                    var options = new TelegramRawLongPollingOptions
                    {
                        AllowedUpdates = ["message", "callback_query"]
                    };

                    return polling.RunAsync(HandleAsync, options, cancellationToken);
                }

                private static async Task StreamAsync(ITelegramLongPollingClient polling, CancellationToken cancellationToken)
                {
                    await foreach (TelegramPolledUpdate item in polling.GetUpdatesAsync(cancellationToken: cancellationToken))
                    {
                        Update update = item.Update;
                        _ = update.UpdateId;
                        await item.AcknowledgeAsync(cancellationToken);
                    }
                }

                private static Task HandleAsync(Update update, CancellationToken cancellationToken)
                {
                    _ = update.UpdateId;
                    return Task.CompletedTask;
                }
            }
            """;

        var compilation = CreateRawLongPollingOnlyCompilation(source);
        using var stream = new MemoryStream();

        var result = compilation.Emit(stream);

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Diagnostics
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(static diagnostic => diagnostic.ToString())));
    }

    [Fact]
    public void TelegramFrameworkLongPolling_Surface_CompilesWithoutOldTelegramPackage()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using TeleFlow.Telegram;

            public static class FrameworkLongPollingOnlySmoke
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddTelegramBot(options => options.Token = "token");
                    services.AddTelegramHandler<Handler>();
                    services.AddLongPolling(options =>
                    {
                        options.AllowedUpdates = TelegramAllowedUpdates.Only(TelegramUpdateType.Message);
                    });
                }
            }

            internal sealed class Handler : MessageHandler
            {
            }
            """;

        var compilation = CreateFrameworkLongPollingOnlyCompilation(source);
        using var stream = new MemoryStream();

        var result = compilation.Emit(stream);

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Diagnostics
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(static diagnostic => diagnostic.ToString())));
    }

    [Fact]
    public void TelegramWebhooks_RawSurface_CompilesWithoutFrameworkPackage()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using TeleFlow.Telegram;
            using TeleFlow.Telegram.Schema.Types;
            using TeleFlow.Telegram.Webhooks;

            public static class RawWebhookOnlySmoke
            {
                public static RouteHandlerBuilder Configure(WebApplication app)
                {
                    return app.MapTelegramWebhook(
                        "/telegram",
                        async (Update update, ITelegramClient bot, CancellationToken cancellationToken) =>
                        {
                            _ = update.UpdateId;
                            _ = bot.Defaults;
                            await Task.Yield();
                            return Results.Ok();
                        },
                        options =>
                        {
                            options.SecretToken = "secret";
                            options.InvalidPayloadStatusCode = StatusCodes.Status422UnprocessableEntity;
                            options.SecretTokenFailureStatusCode = StatusCodes.Status403Forbidden;
                        });
                }
            }
            """;

        var compilation = CreateRawWebhooksOnlyCompilation(source);
        using var stream = new MemoryStream();

        var result = compilation.Emit(stream);

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Diagnostics
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(static diagnostic => diagnostic.ToString())));
    }

    [Fact]
    public void TelegramFrameworkWebhooks_Surface_CompilesWithoutOldTelegramPackage()
    {
        const string source = """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.Extensions.DependencyInjection;
            using TeleFlow.Telegram;
            using TeleFlow.Telegram.Webhooks;

            public static class FrameworkWebhookOnlySmoke
            {
                public static void Configure(IServiceCollection services, WebApplication app)
                {
                    services.AddTelegramBot(options => options.Token = "token");
                    services.AddTelegramHandler<Handler>();
                    services.AddWebhook(options =>
                    {
                        options.Path = "/bot/hook";
                        options.SecretToken = "secret";
                    });

                    app.MapTelegramWebhook();
                }
            }

            internal sealed class Handler : MessageHandler
            {
            }
            """;

        var compilation = CreateFrameworkWebhooksOnlyCompilation(source);
        using var stream = new MemoryStream();

        var result = compilation.Emit(stream);

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Diagnostics
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(static diagnostic => diagnostic.ToString())));
    }

    [Fact]
    public void TelegramDefaultPackage_Surface_CompilesWithClientOnlyClosure()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using System.Threading;
            using System.Threading.Tasks;
            using TeleFlow.Telegram;
            using TeleFlow.Telegram.Schema.Types;

            public static class TelegramDefaultPackageSmoke
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddTelegramClient(options => options.Token = "token");
                }

                public static Task<User> CallAsync(ITelegramClient bot, CancellationToken cancellationToken)
                {
                    TelegramUpdateType updateType = TelegramUpdateType.Message;
                    _ = updateType.Value;

                    return bot.GetMeAsync(cancellationToken);
                }
            }
            """;

        var compilation = CreateTelegramDefaultPackageCompilation(source);
        using var stream = new MemoryStream();

        var result = compilation.Emit(stream);

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Diagnostics
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(static diagnostic => diagnostic.ToString())));
    }

    [Fact]
    public void TelegramFramework_PublicSurface_DoesNotExposeRawTransportPackageTypes()
    {
        var publicTypes = LoadProjectAssembly("TeleFlow.Telegram.Framework")
            .GetExportedTypes()
            .ToArray();

        Assert.DoesNotContain(publicTypes, static type =>
            type.Namespace?.StartsWith("TeleFlow.Telegram.LongPolling", StringComparison.Ordinal) == true ||
            type.Namespace?.StartsWith("TeleFlow.Telegram.Webhooks", StringComparison.Ordinal) == true);
    }

    private static Assembly LoadProjectAssembly(string assemblyName)
    {
        var assemblyPath = Path.Combine(
            AppContext.BaseDirectory,
            $"{assemblyName}.dll");

        return Assembly.LoadFile(assemblyPath);
    }

    private static CSharpCompilation CreateClientOnlyCompilation(string source)
    {
        var references = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedAssemblies
            ? trustedAssemblies
                .Split(Path.PathSeparator)
                .Where(static path => !IsTeleFlowAssemblyReference(path))
                .Select(static path => MetadataReference.CreateFromFile(path))
                .Cast<MetadataReference>()
                .ToList()
            : [];

        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Client").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Schema").Location));
        Assert.Equal(ClientOnlyTeleFlowReferenceFileNames, GetTeleFlowReferenceFileNames(references));

        return CSharpCompilation.Create(
            "TelegramClientOnlySmoke",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static CSharpCompilation CreateFrameworkOnlyCompilation(string source)
    {
        var references = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedAssemblies
            ? trustedAssemblies
                .Split(Path.PathSeparator)
                .Where(static path => !IsTeleFlowAssemblyReference(path))
                .Select(static path => MetadataReference.CreateFromFile(path))
                .Cast<MetadataReference>()
                .ToList()
            : [];

        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Annotations").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Core").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Client").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Framework").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Schema").Location));
        Assert.Equal(FrameworkOnlyTeleFlowReferenceFileNames, GetTeleFlowReferenceFileNames(references));

        return CSharpCompilation.Create(
            "TelegramFrameworkOnlySmoke",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static CSharpCompilation CreateRawLongPollingOnlyCompilation(string source)
    {
        var references = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedAssemblies
            ? trustedAssemblies
                .Split(Path.PathSeparator)
                .Where(static path => !IsTeleFlowAssemblyReference(path))
                .Select(static path => MetadataReference.CreateFromFile(path))
                .Cast<MetadataReference>()
                .ToList()
            : [];

        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Client").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.LongPolling").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Schema").Location));
        Assert.Equal(RawLongPollingOnlyTeleFlowReferenceFileNames, GetTeleFlowReferenceFileNames(references));

        return CSharpCompilation.Create(
            "TelegramRawLongPollingOnlySmoke",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static CSharpCompilation CreateFrameworkLongPollingOnlyCompilation(string source)
    {
        var references = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedAssemblies
            ? trustedAssemblies
                .Split(Path.PathSeparator)
                .Where(static path => !IsTeleFlowAssemblyReference(path))
                .Select(static path => MetadataReference.CreateFromFile(path))
                .Cast<MetadataReference>()
                .ToList()
            : [];

        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Annotations").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Core").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Client").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Framework").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Framework.LongPolling").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.LongPolling").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Schema").Location));
        Assert.Equal(FrameworkLongPollingOnlyTeleFlowReferenceFileNames, GetTeleFlowReferenceFileNames(references));

        return CSharpCompilation.Create(
            "TelegramFrameworkLongPollingOnlySmoke",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static CSharpCompilation CreateRawWebhooksOnlyCompilation(string source)
    {
        var references = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedAssemblies
            ? trustedAssemblies
                .Split(Path.PathSeparator)
                .Where(static path => !IsTeleFlowAssemblyReference(path))
                .Select(static path => MetadataReference.CreateFromFile(path))
                .Cast<MetadataReference>()
                .ToList()
            : [];

        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Client").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Schema").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Webhooks").Location));
        Assert.Equal(RawWebhooksOnlyTeleFlowReferenceFileNames, GetTeleFlowReferenceFileNames(references));

        return CSharpCompilation.Create(
            "TelegramRawWebhooksOnlySmoke",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static CSharpCompilation CreateFrameworkWebhooksOnlyCompilation(string source)
    {
        var references = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedAssemblies
            ? trustedAssemblies
                .Split(Path.PathSeparator)
                .Where(static path => !IsTeleFlowAssemblyReference(path))
                .Select(static path => MetadataReference.CreateFromFile(path))
                .Cast<MetadataReference>()
                .ToList()
            : [];

        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Annotations").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Core").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Client").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Framework").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Framework.Webhooks").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Schema").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Webhooks").Location));
        Assert.Equal(FrameworkWebhooksOnlyTeleFlowReferenceFileNames, GetTeleFlowReferenceFileNames(references));

        return CSharpCompilation.Create(
            "TelegramFrameworkWebhooksOnlySmoke",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static CSharpCompilation CreateTelegramDefaultPackageCompilation(string source)
    {
        var references = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedAssemblies
            ? trustedAssemblies
                .Split(Path.PathSeparator)
                .Where(static path => !IsTeleFlowAssemblyReference(path))
                .Select(static path => MetadataReference.CreateFromFile(path))
                .Cast<MetadataReference>()
                .ToList()
            : [];

        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Client").Location));
        references.Add(MetadataReference.CreateFromFile(LoadProjectAssembly("TeleFlow.Telegram.Schema").Location));
        Assert.Equal(TelegramDefaultPackageTeleFlowReferenceFileNames, GetTeleFlowReferenceFileNames(references));

        return CSharpCompilation.Create(
            "TelegramDefaultPackageSmoke",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static string[] GetTeleFlowReferenceFileNames(IEnumerable<MetadataReference> references)
    {
        return references
            .Select(static reference => reference.Display)
            .OfType<string>()
            .Select(static display => Path.GetFileName(display))
            .OfType<string>()
            .Where(static fileName => fileName.StartsWith("TeleFlow.", StringComparison.Ordinal))
            .OrderBy(static fileName => fileName, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsTeleFlowAssemblyReference(string path)
    {
        return Path.GetFileName(path).StartsWith("TeleFlow.", StringComparison.Ordinal);
    }

    private static string[] GetInternalsVisibleToAssemblyNames(Assembly assembly)
    {
        return assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(static attribute => attribute.AssemblyName.Split(',')[0])
            .ToArray();
    }

    private static bool IsFrameworkRuntimeType(Type type)
    {
        var name = type.Name;

        return name.Contains("Context", StringComparison.Ordinal) ||
            name.Contains("Handler", StringComparison.Ordinal) ||
            name.Contains("Dispatcher", StringComparison.Ordinal) ||
            name.Contains("UpdateSource", StringComparison.Ordinal) ||
            string.Equals(name, "IUpdateProcessor", StringComparison.Ordinal) ||
            string.Equals(name, "IUpdatePayload", StringComparison.Ordinal) ||
            string.Equals(name, "TelegramUpdatePayload", StringComparison.Ordinal);
    }
}
