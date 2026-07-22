using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TeleFlow.ArchitectureTests;

public sealed class PackageSmokeTests : IClassFixture<PackageSmokeFixture>
{
    private const string PackageSmokeCategory = "PackageSmoke";
    private static readonly string PackageVersion = $"1.0.0-smoke.{Environment.ProcessId}";
    private const string NuGetOrgSource = "https://api.nuget.org/v3/index.json";
    private static readonly TimeSpan DotNetCommandTimeout = TimeSpan.FromMinutes(3);

    private static readonly string RepositoryRoot = ResolveRepositoryRoot();

    private static readonly PackageProject[] RuntimePackageProjects =
    [
        new("IWF.TeleFlow.Annotations"),
        new("IWF.TeleFlow.Framework.Core"),
        new("IWF.TeleFlow.Framework.Hosting"),
        new("IWF.TeleFlow.Framework.I18n"),
        new("IWF.TeleFlow.Framework.I18n.Fluent"),
        new("IWF.TeleFlow.Storage.Memory"),
        new("IWF.TeleFlow.Telegram.Schema"),
        new("IWF.TeleFlow.Telegram.Client"),
        new("IWF.TeleFlow.Framework"),
        new("IWF.TeleFlow.Telegram.LongPolling"),
        new("IWF.TeleFlow.Telegram.Webhooks"),
        new("IWF.TeleFlow.Framework.LongPolling"),
        new("IWF.TeleFlow.Framework.Webhooks"),
        new("IWF.TeleFlow.Telegram")
    ];

    private static readonly PackageProject[] ReleaseAlignedToolingPackageProjects =
    [
        new("IWF.TeleFlow.Generators")
    ];

    private static readonly PackageProject[] AllPackageProjects =
    [
        .. RuntimePackageProjects,
        .. ReleaseAlignedToolingPackageProjects
    ];

    private readonly PackageSmokeFixture _fixture;

    public PackageSmokeTests(PackageSmokeFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", PackageSmokeCategory)]
    public async Task DocumentedPackageReferences_RestoreBuildAndResolveExpectedDependencyClosure()
    {
        var tempDirectory = _fixture.TempDirectory;
        var packageSource = _fixture.PackageSource;

        AssertPackedPackageSet(packageSource, AllPackageProjects);
        AssertPackedPackageMetadata(packageSource, RuntimePackageProjects);

        var scenarios = CreateScenarios();

        foreach (var scenario in scenarios)
        {
            await BuildConsumerAsync(tempDirectory.FullName, packageSource, scenario);

            var packageNames = ReadResolvedPackageNames(
                Path.Combine(tempDirectory.FullName, scenario.Name, "obj", "project.assets.json"));

            Assert.All(scenario.ExpectedPackages, packageName => Assert.Contains(packageName, packageNames));
            Assert.All(scenario.ForbiddenPackages, packageName => Assert.DoesNotContain(packageName, packageNames));

            foreach (var optionalI18nPackage in new[]
                     {
                         "IWF.TeleFlow.Framework.I18n",
                         "IWF.TeleFlow.Framework.I18n.Fluent"
                     })
            {
                if (!scenario.ExpectedPackages.Contains(optionalI18nPackage, StringComparer.Ordinal))
                {
                    Assert.DoesNotContain(optionalI18nPackage, packageNames);
                }
            }
        }
    }

    [Fact]
    [Trait("Category", PackageSmokeCategory)]
    public void ReleaseAlignedToolingPackages_PackWithVersionAndMetadata()
    {
        var packageSource = _fixture.PackageSource;
        AssertPackedPackageMetadata(packageSource, ReleaseAlignedToolingPackageProjects);
        AssertPackedAnalyzerPackage(packageSource, "IWF.TeleFlow.Generators");
    }

    [Fact]
    [Trait("Category", PackageSmokeCategory)]
    public async Task ReleaseAlignedToolingPackages_LoadAsAnalyzersFromPackageReference()
    {
        var tempDirectory = _fixture.TempDirectory;
        var packageSource = _fixture.PackageSource;

        foreach (var scenario in CreateGeneratedPackageConsumerScenarios())
        {
            await BuildGeneratorPackageConsumerAsync(tempDirectory.FullName, packageSource, scenario);
        }
    }

    [Fact]
    [Trait("Category", PackageSmokeCategory)]
    public void VerifyReleaseScript_UsesTheSamePackageInventory()
    {
        var scriptPath = Path.Combine(RepositoryRoot, "eng", "verify-release.ps1");
        var script = File.ReadAllText(scriptPath);

        var packageIds = Regex
            .Matches(script, "Id\\s*=\\s*\"(?<id>IWF\\.TeleFlow\\.[^\"]+)\"")
            .Select(static match => match.Groups["id"].Value)
            .ToHashSet(StringComparer.Ordinal);

        var expectedPackageIds = RuntimePackageProjects
            .Concat(ReleaseAlignedToolingPackageProjects)
            .Select(static project => project.PackageId)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            expectedPackageIds.Order(StringComparer.Ordinal),
            packageIds.Order(StringComparer.Ordinal));
    }

    internal static Task PackPackageTrainAsync(string packageSource)
    {
        return RunDotNetAsync(
            RepositoryRoot,
            "pack",
            Path.Combine(RepositoryRoot, "TeleFlow.sln"),
            "--configuration",
            "Release",
            "--no-restore",
            "--output",
            packageSource,
            "/p:PackageVersion=" + PackageVersion,
            "/nodeReuse:false");
    }

    private static async Task BuildConsumerAsync(
        string rootDirectory,
        string packageSource,
        PackageConsumerScenario scenario)
    {
        var consumerDirectory = Directory.CreateDirectory(Path.Combine(rootDirectory, scenario.Name));
        var projectPath = Path.Combine(consumerDirectory.FullName, scenario.Name + ".csproj");

        await File.WriteAllTextAsync(projectPath, CreateProjectFile(scenario));
        await File.WriteAllTextAsync(Path.Combine(consumerDirectory.FullName, "Smoke.cs"), scenario.Source);

        await RunDotNetAsync(
            consumerDirectory.FullName,
            "build",
            projectPath,
            "--configuration",
            "Release",
            "--source",
            packageSource,
            "--source",
            NuGetOrgSource,
            "/p:RestoreIgnoreFailedSources=true",
            "/p:RestoreNoCache=true",
            "/nodeReuse:false");
    }

    private static async Task BuildGeneratorPackageConsumerAsync(
        string rootDirectory,
        string packageSource,
        GeneratedPackageConsumerScenario scenario)
    {
        var consumerDirectory = Directory.CreateDirectory(Path.Combine(rootDirectory, scenario.Name));
        var projectPath = Path.Combine(consumerDirectory.FullName, scenario.Name + ".csproj");

        await File.WriteAllTextAsync(projectPath, CreateGeneratorPackageConsumerProjectFile(scenario));
        await File.WriteAllTextAsync(Path.Combine(consumerDirectory.FullName, "GeneratorSmoke.cs"), scenario.Source);

        await RunDotNetAsync(
            consumerDirectory.FullName,
            "build",
            projectPath,
            "--configuration",
            "Release",
            "--source",
            packageSource,
            "--source",
            NuGetOrgSource,
            "/p:RestoreIgnoreFailedSources=true",
            "/p:RestoreNoCache=true",
            "/nodeReuse:false");
    }

    private static void AssertPackedPackageSet(
        string packageSource,
        IReadOnlyList<PackageProject> packageProjects)
    {
        var packages = Directory
            .GetFiles(packageSource, "*.nupkg")
            .Select(ReadPackageIdentity)
            .ToDictionary(static package => package.Id, static package => package.Version, StringComparer.Ordinal);

        Assert.Equal(
            packageProjects.Select(static project => project.PackageId).Order(StringComparer.Ordinal),
            packages.Keys.Order(StringComparer.Ordinal));

        Assert.All(packages, static package => Assert.Equal(PackageVersion, package.Value));
    }

    private static void AssertPackedPackageMetadata(
        string packageSource,
        IReadOnlyList<PackageProject> packageProjects)
    {
        foreach (var packageProject in packageProjects)
        {
            var packagePath = Path.Combine(packageSource, $"{packageProject.PackageId}.{PackageVersion}.nupkg");
            var package = ReadPackageIdentity(packagePath);

            Assert.Equal("IWFTech", package.Authors);
            Assert.False(string.IsNullOrWhiteSpace(package.Description));
            AssertPackageDescriptionPolicy(package);
            Assert.Contains("teleflow", package.Tags, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("README.md", package.Readme);

            using var archive = ZipFile.OpenRead(packagePath);
            Assert.Contains(archive.Entries, static entry => entry.FullName == "README.md");

            if (string.Equals(packageProject.PackageId, "IWF.TeleFlow.Annotations", StringComparison.Ordinal))
            {
                Assert.Contains(
                    archive.Entries,
                    static entry => entry.FullName == "lib/net10.0/TeleFlow.Annotations.xml");
            }
        }
    }

    private static void AssertPackageDescriptionPolicy(PackageIdentity package)
    {
        var requiredText = package.Id switch
        {
            "IWF.TeleFlow.Framework.LongPolling" or
            "IWF.TeleFlow.Framework.Webhooks" or
            "IWF.TeleFlow.Telegram" => "Recommended TeleFlow",

            "IWF.TeleFlow.Framework.Core" or
            "IWF.TeleFlow.Framework" or
            "IWF.TeleFlow.Telegram.Client" or
            "IWF.TeleFlow.Telegram.Schema" or
            "IWF.TeleFlow.Annotations" => "Dependency package",

            "IWF.TeleFlow.Telegram.LongPolling" or
            "IWF.TeleFlow.Telegram.Webhooks" => "Advanced TeleFlow raw transport package",

            "IWF.TeleFlow.Framework.Hosting" or
            "IWF.TeleFlow.Framework.I18n" or
            "IWF.TeleFlow.Framework.I18n.Fluent" => "Optional TeleFlow",
            "IWF.TeleFlow.Generators" => "Reference directly with PrivateAssets=all",
            "IWF.TeleFlow.Storage.Memory" => "TeleFlow state storage add-on",
            _ => throw new InvalidOperationException($"Unexpected package id '{package.Id}'.")
        };

        Assert.Contains(requiredText, package.Description, StringComparison.Ordinal);
    }

    private static void AssertPackedAnalyzerPackage(string packageSource, string packageId)
    {
        var packagePath = Path.Combine(packageSource, $"{packageId}.{PackageVersion}.nupkg");

        using var archive = ZipFile.OpenRead(packagePath);

        Assert.Contains(
            archive.Entries,
            static entry => entry.FullName == "analyzers/dotnet/cs/TeleFlow.Generators.dll");

        Assert.DoesNotContain(
            archive.Entries,
            static entry => entry.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase));
    }

    private static PackageIdentity ReadPackageIdentity(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var nuspec = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));

        using var stream = nuspec.Open();
        var document = XDocument.Load(stream);

        var metadata = document
            .Descendants()
            .Single(element => element.Name.LocalName == "metadata");

        return new PackageIdentity(
            ReadMetadataValue(metadata, "id"),
            ReadMetadataValue(metadata, "version"),
            ReadMetadataValue(metadata, "description"),
            ReadMetadataValue(metadata, "authors"),
            ReadMetadataValue(metadata, "tags"),
            ReadMetadataValue(metadata, "readme"));
    }

    private static string ReadMetadataValue(XElement metadata, string name)
    {
        return metadata
            .Elements()
            .Single(element => element.Name.LocalName == name)
            .Value;
    }

    private static string CreateProjectFile(PackageConsumerScenario scenario)
    {
        var frameworkReference = scenario.RequiresAspNetCore
            ? """
                <ItemGroup>
                  <FrameworkReference Include="Microsoft.AspNetCore.App" />
                </ItemGroup>

                """
            : string.Empty;

        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="{scenario.PackageId}" Version="{PackageVersion}" />
              </ItemGroup>

            {frameworkReference}</Project>
            """;
    }

    private static string CreateGeneratorPackageConsumerProjectFile(GeneratedPackageConsumerScenario scenario)
    {
        var frameworkReference = scenario.RequiresAspNetCore
            ? """
                <ItemGroup>
                  <FrameworkReference Include="Microsoft.AspNetCore.App" />
                </ItemGroup>

                """
            : string.Empty;

        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="{scenario.PackageId}" Version="{PackageVersion}" />
                <PackageReference Include="IWF.TeleFlow.Generators" Version="{PackageVersion}" PrivateAssets="all" />
              </ItemGroup>

            {frameworkReference}</Project>
            """;
    }

    private static IReadOnlyList<GeneratedPackageConsumerScenario> CreateGeneratedPackageConsumerScenarios()
    {
        return
        [
            new GeneratedPackageConsumerScenario(
                Name: "FrameworkLongPollingGeneratedPackageConsumer",
                PackageId: "IWF.TeleFlow.Framework.LongPolling",
                RequiresAspNetCore: false,
                Source:
                """
                using Microsoft.Extensions.DependencyInjection;
                using TeleFlow.Annotations;
                using TeleFlow.Telegram;

                namespace FrameworkLongPollingGeneratedPackageConsumer;

                public static class BotRegistration
                {
                    public static void Configure(IServiceCollection services)
                    {
                        services.AddTelegramBot(options => options.Token = "token");
                        services.AddTelegramHandlersFromAssembly(typeof(BotRegistration).Assembly);
                        services.AddLongPolling();

                        _ = typeof(TeleFlow.Generated.TelegramGeneratedHandlersRegistrar);
                    }
                }

                public sealed class StartHandler
                {
                    [Command("start")]
                    public Task Handle(MessageContext context)
                    {
                        _ = context.Message;
                        return Task.CompletedTask;
                    }
                }
                """),

            new GeneratedPackageConsumerScenario(
                Name: "FrameworkWebhooksGeneratedPackageConsumer",
                PackageId: "IWF.TeleFlow.Framework.Webhooks",
                RequiresAspNetCore: true,
                Source:
                """
                using Microsoft.AspNetCore.Builder;
                using Microsoft.Extensions.DependencyInjection;
                using TeleFlow.Annotations;
                using TeleFlow.Telegram;
                using TeleFlow.Telegram.Webhooks;

                namespace FrameworkWebhooksGeneratedPackageConsumer;

                public static class BotRegistration
                {
                    public static void Configure(IServiceCollection services, WebApplication app)
                    {
                        services.AddTelegramBot(options => options.Token = "token");
                        services.AddTelegramHandlersFromAssembly(typeof(BotRegistration).Assembly);
                        services.AddWebhook(options => options.Path = "/bot/hook");

                        app.MapTelegramWebhook();

                        _ = typeof(TeleFlow.Generated.TelegramGeneratedHandlersRegistrar);
                    }
                }

                public sealed class StartHandler
                {
                    [Command("start")]
                    public Task Handle(MessageContext context)
                    {
                        _ = context.Message;
                        return Task.CompletedTask;
                    }
                }
                """)
        ];
    }

    private static IReadOnlySet<string> ReadResolvedPackageNames(string assetsPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));

        return document.RootElement
            .GetProperty("libraries")
            .EnumerateObject()
            .Select(static property => property.Name.Split('/')[0])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<PackageConsumerScenario> CreateScenarios()
    {
        return
        [
            new PackageConsumerScenario(
                Name: "MemoryStoragePackageConsumer",
                PackageId: "IWF.TeleFlow.Storage.Memory",
                RequiresAspNetCore: false,
                Source:
                """
                using Microsoft.Extensions.DependencyInjection;
                using TeleFlow.Framework.States;
                using TeleFlow.Storage.Memory;

                public static class MemoryStoragePackageConsumer
                {
                    public static void Configure(IServiceCollection services)
                    {
                        services.AddMemoryStateStorage();
                    }

                    public static async Task UseAsync(IStateStore stateStore, CancellationToken cancellationToken)
                    {
                        var key = new StateKey("chat", "42");
                        await stateStore.SetStateAsync(key, "opened", cancellationToken);
                        _ = await stateStore.GetStateAsync(key, cancellationToken);
                    }
                }
                """,
                ExpectedPackages:
                [
                    "IWF.TeleFlow.Framework.Core",
                    "IWF.TeleFlow.Storage.Memory"
                ],
                ForbiddenPackages:
                [
                    "IWF.TeleFlow.Annotations",
                    "IWF.TeleFlow.Framework.Hosting",
                    "IWF.TeleFlow.Telegram",
                    "IWF.TeleFlow.Telegram.Client",
                    "IWF.TeleFlow.Framework",
                    "IWF.TeleFlow.Framework.LongPolling",
                    "IWF.TeleFlow.Framework.Webhooks",
                    "IWF.TeleFlow.Telegram.LongPolling",
                    "IWF.TeleFlow.Telegram.Schema",
                    "IWF.TeleFlow.Telegram.Webhooks"
                ]),

            new PackageConsumerScenario(
                Name: "HostingPackageConsumer",
                PackageId: "IWF.TeleFlow.Framework.Hosting",
                RequiresAspNetCore: false,
                Source:
                """
                using System.Collections.Generic;
                using System.Linq;
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.Extensions.Hosting;
                using TeleFlow.Framework.Hosting;

                public static class HostingPackageConsumer
                {
                    public static void Configure(IServiceCollection services)
                    {
                        services.AddTeleFlowHostedService();
                    }

                    public static int CountHostedServices(IEnumerable<IHostedService> services)
                    {
                        return services.Count();
                    }
                }
                """,
                ExpectedPackages:
                [
                    "IWF.TeleFlow.Framework.Core",
                    "IWF.TeleFlow.Framework.Hosting"
                ],
                ForbiddenPackages:
                [
                    "IWF.TeleFlow.Annotations",
                    "IWF.TeleFlow.Storage.Memory",
                    "IWF.TeleFlow.Telegram",
                    "IWF.TeleFlow.Telegram.Client",
                    "IWF.TeleFlow.Framework",
                    "IWF.TeleFlow.Framework.LongPolling",
                    "IWF.TeleFlow.Framework.Webhooks",
                    "IWF.TeleFlow.Telegram.LongPolling",
                    "IWF.TeleFlow.Telegram.Schema",
                    "IWF.TeleFlow.Telegram.Webhooks"
                ]),

            new PackageConsumerScenario(
                Name: "FrameworkI18nPackageConsumer",
                PackageId: "IWF.TeleFlow.Framework.I18n",
                RequiresAspNetCore: false,
                Source:
                """
                using Microsoft.Extensions.DependencyInjection;
                using TeleFlow.Telegram;
                using TeleFlow.Telegram.I18n;

                public static class FrameworkI18nPackageConsumer
                {
                    public static void Configure(IServiceCollection services)
                    {
                        services.AddTelegramBot(options => options.Token = "token");
                        services.AddTelegramI18n(options => options.FallbackLocale = new Locale("en"));
                        services.AddTelegramLocaleResolver<ApplicationLocaleResolver>();
                    }
                }

                internal sealed class ApplicationLocaleResolver : ILocaleResolver
                {
                    public ValueTask<Locale?> TryResolveAsync(
                        LocaleResolutionContext context,
                        CancellationToken cancellationToken = default)
                    {
                        return ValueTask.FromResult<Locale?>(null);
                    }
                }
                """,
                ExpectedPackages:
                [
                    "IWF.TeleFlow.Annotations",
                    "IWF.TeleFlow.Framework.Core",
                    "IWF.TeleFlow.Framework",
                    "IWF.TeleFlow.Framework.I18n",
                    "IWF.TeleFlow.Telegram.Client",
                    "IWF.TeleFlow.Telegram.Schema"
                ],
                ForbiddenPackages:
                [
                    "IWF.TeleFlow.Framework.Hosting",
                    "IWF.TeleFlow.Framework.I18n.Fluent",
                    "IWF.TeleFlow.Framework.LongPolling",
                    "IWF.TeleFlow.Framework.Webhooks",
                    "IWF.TeleFlow.Storage.Memory",
                    "IWF.TeleFlow.Telegram",
                    "IWF.TeleFlow.Telegram.LongPolling",
                    "IWF.TeleFlow.Telegram.Webhooks"
                ]),

            new PackageConsumerScenario(
                Name: "FrameworkI18nFluentPackageConsumer",
                PackageId: "IWF.TeleFlow.Framework.I18n.Fluent",
                RequiresAspNetCore: false,
                Source:
                """
                using Microsoft.Extensions.DependencyInjection;
                using TeleFlow.Telegram;
                using TeleFlow.Telegram.I18n;
                using TeleFlow.Telegram.I18n.Fluent;

                public static class FrameworkI18nFluentPackageConsumer
                {
                    public static void Configure(IServiceCollection services)
                    {
                        services.AddTelegramBot(options => options.Token = "token");
                        services.AddTelegramFluentI18n(options =>
                        {
                            options.ResourcesPath = "Locales";
                            options.FallbackLocale = new Locale("en");
                        });
                    }

                    public static string Format(IFluentTextFormatter formatter)
                    {
                        return formatter.Format(new Locale("en"), "welcome", ("name", "User"));
                    }
                }
                """,
                ExpectedPackages:
                [
                    "IWF.TeleFlow.Annotations",
                    "IWF.TeleFlow.Framework.Core",
                    "IWF.TeleFlow.Framework",
                    "IWF.TeleFlow.Framework.I18n",
                    "IWF.TeleFlow.Framework.I18n.Fluent",
                    "IWF.TeleFlow.Telegram.Client",
                    "IWF.TeleFlow.Telegram.Schema"
                ],
                ForbiddenPackages:
                [
                    "IWF.TeleFlow.Framework.Hosting",
                    "IWF.TeleFlow.Framework.LongPolling",
                    "IWF.TeleFlow.Framework.Webhooks",
                    "IWF.TeleFlow.Storage.Memory",
                    "IWF.TeleFlow.Telegram",
                    "IWF.TeleFlow.Telegram.LongPolling",
                    "IWF.TeleFlow.Telegram.Webhooks"
                ]),

            new PackageConsumerScenario(
                Name: "ClientOnlyPackageConsumer",
                PackageId: "IWF.TeleFlow.Telegram.Client",
                RequiresAspNetCore: false,
                Source:
                """
                using Microsoft.Extensions.DependencyInjection;
                using TeleFlow.Telegram;
                using TeleFlow.Telegram.Schema.Types;

                public static class ClientOnlyPackageConsumer
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
                """,
                ExpectedPackages:
                [
                    "IWF.TeleFlow.Telegram.Client",
                    "IWF.TeleFlow.Telegram.Schema"
                ],
                ForbiddenPackages:
                [
                    "IWF.TeleFlow.Annotations",
                    "IWF.TeleFlow.Framework.Core",
                    "IWF.TeleFlow.Framework.Hosting",
                    "IWF.TeleFlow.Telegram",
                    "IWF.TeleFlow.Framework",
                    "IWF.TeleFlow.Framework.LongPolling",
                    "IWF.TeleFlow.Framework.Webhooks",
                    "IWF.TeleFlow.Telegram.LongPolling",
                    "IWF.TeleFlow.Telegram.Webhooks"
                ]),

            new PackageConsumerScenario(
                Name: "RawLongPollingPackageConsumer",
                PackageId: "IWF.TeleFlow.Telegram.LongPolling",
                RequiresAspNetCore: false,
                Source:
                """
                using TeleFlow.Telegram;
                using TeleFlow.Telegram.Schema.Types;

                public static class RawLongPollingPackageConsumer
                {
                    public static Task RunAsync(ITelegramLongPollingClient polling, CancellationToken cancellationToken)
                    {
                        var options = new TelegramRawLongPollingOptions
                        {
                            AllowedUpdates = ["message", "callback_query"]
                        };

                        return polling.RunAsync(HandleAsync, options, cancellationToken);
                    }

                    private static Task HandleAsync(Update update, CancellationToken cancellationToken)
                    {
                        _ = update.UpdateId;
                        return Task.CompletedTask;
                    }
                }
                """,
                ExpectedPackages:
                [
                    "IWF.TeleFlow.Telegram.Client",
                    "IWF.TeleFlow.Telegram.LongPolling",
                    "IWF.TeleFlow.Telegram.Schema"
                ],
                ForbiddenPackages:
                [
                    "IWF.TeleFlow.Annotations",
                    "IWF.TeleFlow.Framework.Core",
                    "IWF.TeleFlow.Framework.Hosting",
                    "IWF.TeleFlow.Telegram",
                    "IWF.TeleFlow.Framework",
                    "IWF.TeleFlow.Framework.LongPolling",
                    "IWF.TeleFlow.Framework.Webhooks",
                    "IWF.TeleFlow.Telegram.Webhooks"
                ]),

            new PackageConsumerScenario(
                Name: "RawWebhooksPackageConsumer",
                PackageId: "IWF.TeleFlow.Telegram.Webhooks",
                RequiresAspNetCore: true,
                Source:
                """
                using Microsoft.AspNetCore.Builder;
                using Microsoft.AspNetCore.Http;
                using TeleFlow.Telegram;
                using TeleFlow.Telegram.Schema.Types;
                using TeleFlow.Telegram.Webhooks;

                public static class RawWebhooksPackageConsumer
                {
                    public static RouteHandlerBuilder Configure(WebApplication app)
                    {
                        return app.MapTelegramWebhook(
                            "/telegram",
                            (Update update, ITelegramClient bot, CancellationToken cancellationToken) =>
                            {
                                _ = update.UpdateId;
                                _ = bot.Defaults;
                                return Task.FromResult(Results.Ok());
                            });
                    }
                }
                """,
                ExpectedPackages:
                [
                    "IWF.TeleFlow.Telegram.Client",
                    "IWF.TeleFlow.Telegram.Schema",
                    "IWF.TeleFlow.Telegram.Webhooks"
                ],
                ForbiddenPackages:
                [
                    "IWF.TeleFlow.Annotations",
                    "IWF.TeleFlow.Framework.Core",
                    "IWF.TeleFlow.Framework.Hosting",
                    "IWF.TeleFlow.Telegram",
                    "IWF.TeleFlow.Framework",
                    "IWF.TeleFlow.Framework.LongPolling",
                    "IWF.TeleFlow.Framework.Webhooks",
                    "IWF.TeleFlow.Telegram.LongPolling"
                ]),

            new PackageConsumerScenario(
                Name: "FrameworkLongPollingPackageConsumer",
                PackageId: "IWF.TeleFlow.Framework.LongPolling",
                RequiresAspNetCore: false,
                Source:
                """
                using Microsoft.Extensions.DependencyInjection;
                using TeleFlow.Telegram;

                public static class FrameworkLongPollingPackageConsumer
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
                """,
                ExpectedPackages:
                [
                    "IWF.TeleFlow.Annotations",
                    "IWF.TeleFlow.Framework.Core",
                    "IWF.TeleFlow.Telegram.Client",
                    "IWF.TeleFlow.Framework",
                    "IWF.TeleFlow.Framework.LongPolling",
                    "IWF.TeleFlow.Telegram.LongPolling",
                    "IWF.TeleFlow.Telegram.Schema"
                ],
                ForbiddenPackages:
                [
                    "IWF.TeleFlow.Framework.Hosting",
                    "IWF.TeleFlow.Telegram",
                    "IWF.TeleFlow.Framework.Webhooks",
                    "IWF.TeleFlow.Telegram.Webhooks"
                ]),

            new PackageConsumerScenario(
                Name: "FrameworkWebhooksPackageConsumer",
                PackageId: "IWF.TeleFlow.Framework.Webhooks",
                RequiresAspNetCore: true,
                Source:
                """
                using Microsoft.AspNetCore.Builder;
                using Microsoft.Extensions.DependencyInjection;
                using TeleFlow.Telegram;
                using TeleFlow.Telegram.Webhooks;

                public static class FrameworkWebhooksPackageConsumer
                {
                    public static void Configure(IServiceCollection services, WebApplication app)
                    {
                        services.AddTelegramBot(options => options.Token = "token");
                        services.AddTelegramHandler<Handler>();
                        services.AddWebhook(options => options.Path = "/bot/hook");

                        app.MapTelegramWebhook();
                    }
                }

                internal sealed class Handler : MessageHandler
                {
                }
                """,
                ExpectedPackages:
                [
                    "IWF.TeleFlow.Annotations",
                    "IWF.TeleFlow.Framework.Core",
                    "IWF.TeleFlow.Telegram.Client",
                    "IWF.TeleFlow.Framework",
                    "IWF.TeleFlow.Framework.Webhooks",
                    "IWF.TeleFlow.Telegram.Schema",
                    "IWF.TeleFlow.Telegram.Webhooks"
                ],
                ForbiddenPackages:
                [
                    "IWF.TeleFlow.Framework.Hosting",
                    "IWF.TeleFlow.Telegram",
                    "IWF.TeleFlow.Framework.LongPolling",
                    "IWF.TeleFlow.Telegram.LongPolling"
                ]),

            new PackageConsumerScenario(
                Name: "TelegramDefaultPackageConsumer",
                PackageId: "IWF.TeleFlow.Telegram",
                RequiresAspNetCore: false,
                Source:
                """
                using Microsoft.Extensions.DependencyInjection;
                using TeleFlow.Telegram;
                using TeleFlow.Telegram.Schema.Types;

                public static class TelegramDefaultPackageConsumer
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
                """,
                ExpectedPackages:
                [
                    "IWF.TeleFlow.Telegram",
                    "IWF.TeleFlow.Telegram.Client",
                    "IWF.TeleFlow.Telegram.Schema"
                ],
                ForbiddenPackages:
                [
                    "IWF.TeleFlow.Annotations",
                    "IWF.TeleFlow.Framework.Core",
                    "IWF.TeleFlow.Framework.Hosting",
                    "IWF.TeleFlow.Framework",
                    "IWF.TeleFlow.Framework.LongPolling",
                    "IWF.TeleFlow.Framework.Webhooks",
                    "IWF.TeleFlow.Telegram.LongPolling",
                    "IWF.TeleFlow.Telegram.Webhooks"
                ])
        ];
    }

    private static async Task RunDotNetAsync(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The dotnet process could not be started.");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        var waitTask = process.WaitForExitAsync();

        if (await Task.WhenAny(waitTask, Task.Delay(DotNetCommandTimeout)) != waitTask)
        {
            process.Kill(entireProcessTree: true);

            var timeoutOutput = await outputTask;
            var timeoutError = await errorTask;

            throw new TimeoutException(
                $"dotnet {string.Join(' ', arguments)} timed out after {DotNetCommandTimeout}.{Environment.NewLine}{timeoutOutput}{Environment.NewLine}{timeoutError}");
        }

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.{Environment.NewLine}{output}{Environment.NewLine}{error}");
        }
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TeleFlow.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be resolved.");
    }

    private sealed record PackageConsumerScenario(
        string Name,
        string PackageId,
        bool RequiresAspNetCore,
        string Source,
        IReadOnlyList<string> ExpectedPackages,
        IReadOnlyList<string> ForbiddenPackages);

    private sealed record GeneratedPackageConsumerScenario(
        string Name,
        string PackageId,
        bool RequiresAspNetCore,
        string Source);

    private sealed record PackageProject(string PackageId);

    private sealed record PackageIdentity(
        string Id,
        string Version,
        string Description,
        string Authors,
        string Tags,
        string Readme);
}

public sealed class PackageSmokeFixture : IAsyncLifetime
{
    private DirectoryInfo? _tempDirectory;
    private string? _packageSource;

    public DirectoryInfo TempDirectory => _tempDirectory
        ?? throw new InvalidOperationException("The package smoke fixture has not been initialized.");

    public string PackageSource => _packageSource
        ?? throw new InvalidOperationException("The package smoke fixture has not been initialized.");

    public async Task InitializeAsync()
    {
        _tempDirectory = Directory.CreateTempSubdirectory("teleflow-package-smoke-");
        _packageSource = Directory.CreateDirectory(Path.Combine(_tempDirectory.FullName, "packages")).FullName;

        await PackageSmokeTests.PackPackageTrainAsync(_packageSource);
    }

    public Task DisposeAsync()
    {
        _tempDirectory?.Delete(recursive: true);
        return Task.CompletedTask;
    }
}
