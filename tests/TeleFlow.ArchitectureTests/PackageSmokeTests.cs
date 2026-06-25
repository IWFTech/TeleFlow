using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace TeleFlow.ArchitectureTests;

public sealed class PackageSmokeTests
{
    private static readonly string PackageVersion = $"1.0.0-smoke.{Environment.ProcessId}";
    private const string NuGetOrgSource = "https://api.nuget.org/v3/index.json";
    private static readonly TimeSpan DotNetCommandTimeout = TimeSpan.FromMinutes(3);

    private static readonly string RepositoryRoot = ResolveRepositoryRoot();

    private static readonly PackageProject[] RuntimePackageProjects =
    [
        new("IWF.TeleFlow.Annotations", "src/TeleFlow.Annotations/TeleFlow.Annotations.csproj"),
        new("IWF.TeleFlow.Core", "src/TeleFlow.Core/TeleFlow.Core.csproj"),
        new("IWF.TeleFlow.Storage.Memory", "src/TeleFlow.Storage.Memory/TeleFlow.Storage.Memory.csproj"),
        new("IWF.TeleFlow.Telegram.Schema", "src/TeleFlow.Telegram.Schema/TeleFlow.Telegram.Schema.csproj"),
        new("IWF.TeleFlow.Telegram.Client", "src/TeleFlow.Telegram.Client/TeleFlow.Telegram.Client.csproj"),
        new("IWF.TeleFlow.Telegram.Framework", "src/TeleFlow.Telegram.Framework/TeleFlow.Telegram.Framework.csproj"),
        new("IWF.TeleFlow.Telegram.LongPolling", "src/TeleFlow.Telegram.LongPolling/TeleFlow.Telegram.LongPolling.csproj"),
        new("IWF.TeleFlow.Telegram.Webhooks", "src/TeleFlow.Telegram.Webhooks/TeleFlow.Telegram.Webhooks.csproj"),
        new("IWF.TeleFlow.Telegram.Framework.LongPolling", "src/TeleFlow.Telegram.Framework.LongPolling/TeleFlow.Telegram.Framework.LongPolling.csproj"),
        new("IWF.TeleFlow.Telegram.Framework.Webhooks", "src/TeleFlow.Telegram.Framework.Webhooks/TeleFlow.Telegram.Framework.Webhooks.csproj"),
        new("IWF.TeleFlow.Telegram", "src/TeleFlow.Telegram/TeleFlow.Telegram.csproj")
    ];

    private static readonly PackageProject[] ReleaseAlignedToolingPackageProjects =
    [
        new("IWF.TeleFlow.Generators", "src/TeleFlow.Generators/TeleFlow.Generators.csproj")
    ];

    [Fact]
    public async Task DocumentedPackageReferences_RestoreBuildAndResolveExpectedDependencyClosure()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("teleflow-package-smoke-");

        try
        {
            var packageSource = Directory.CreateDirectory(Path.Combine(tempDirectory.FullName, "packages"));
            await PackRuntimePackagesAsync(packageSource.FullName);
            AssertPackedRuntimePackageTrain(packageSource.FullName);
            AssertPackedPackageMetadata(packageSource.FullName, RuntimePackageProjects);

            var scenarios = CreateScenarios();

            foreach (var scenario in scenarios)
            {
                await BuildConsumerAsync(tempDirectory.FullName, packageSource.FullName, scenario);

                var packageNames = ReadResolvedPackageNames(
                    Path.Combine(tempDirectory.FullName, scenario.Name, "obj", "project.assets.json"));

                Assert.All(scenario.ExpectedPackages, packageName => Assert.Contains(packageName, packageNames));
                Assert.All(scenario.ForbiddenPackages, packageName => Assert.DoesNotContain(packageName, packageNames));
            }
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ReleaseAlignedToolingPackages_PackWithVersionAndMetadata()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("teleflow-tooling-package-smoke-");

        try
        {
            var packageSource = Directory.CreateDirectory(Path.Combine(tempDirectory.FullName, "packages"));

            foreach (var packageProject in ReleaseAlignedToolingPackageProjects)
            {
                await PackPackageAsync(packageProject, packageSource.FullName);
            }

            AssertPackedPackageSet(packageSource.FullName, ReleaseAlignedToolingPackageProjects);
            AssertPackedPackageMetadata(packageSource.FullName, ReleaseAlignedToolingPackageProjects);
            AssertPackedAnalyzerPackage(packageSource.FullName, "IWF.TeleFlow.Generators");
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ReleaseAlignedToolingPackages_LoadAsAnalyzersFromPackageReference()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("teleflow-generator-package-smoke-");

        try
        {
            var packageSource = Directory.CreateDirectory(Path.Combine(tempDirectory.FullName, "packages"));
            await PackRuntimePackagesAsync(packageSource.FullName);

            foreach (var packageProject in ReleaseAlignedToolingPackageProjects)
            {
                await PackPackageAsync(packageProject, packageSource.FullName);
            }

            foreach (var scenario in CreateGeneratedPackageConsumerScenarios())
            {
                await BuildGeneratorPackageConsumerAsync(tempDirectory.FullName, packageSource.FullName, scenario);
            }
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    private static async Task PackRuntimePackagesAsync(string packageSource)
    {
        foreach (var packageProject in RuntimePackageProjects)
        {
            await PackPackageAsync(packageProject, packageSource);
        }
    }

    private static Task PackPackageAsync(PackageProject packageProject, string packageSource)
    {
        return RunDotNetAsync(
            RepositoryRoot,
            "pack",
            Path.Combine(RepositoryRoot, packageProject.RelativeProjectPath),
            "--configuration",
            "Release",
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

    private static void AssertPackedRuntimePackageTrain(string packageSource)
    {
        AssertPackedPackageSet(packageSource, RuntimePackageProjects);
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
            Assert.Contains("teleflow", package.Tags, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("README.md", package.Readme);

            using var archive = ZipFile.OpenRead(packagePath);
            Assert.Contains(archive.Entries, static entry => entry.FullName == "README.md");
        }
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
                PackageId: "IWF.TeleFlow.Telegram.Framework.LongPolling",
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
                PackageId: "IWF.TeleFlow.Telegram.Framework.Webhooks",
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
                using TeleFlow.Core.States;
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
                    "IWF.TeleFlow.Core",
                    "IWF.TeleFlow.Storage.Memory"
                ],
                ForbiddenPackages:
                [
                    "IWF.TeleFlow.Annotations",
                    "IWF.TeleFlow.Telegram",
                    "IWF.TeleFlow.Telegram.Client",
                    "IWF.TeleFlow.Telegram.Framework",
                    "IWF.TeleFlow.Telegram.Framework.LongPolling",
                    "IWF.TeleFlow.Telegram.Framework.Webhooks",
                    "IWF.TeleFlow.Telegram.LongPolling",
                    "IWF.TeleFlow.Telegram.Schema",
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
                    "IWF.TeleFlow.Core",
                    "IWF.TeleFlow.Telegram",
                    "IWF.TeleFlow.Telegram.Framework",
                    "IWF.TeleFlow.Telegram.Framework.LongPolling",
                    "IWF.TeleFlow.Telegram.Framework.Webhooks",
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
                    "IWF.TeleFlow.Core",
                    "IWF.TeleFlow.Telegram",
                    "IWF.TeleFlow.Telegram.Framework",
                    "IWF.TeleFlow.Telegram.Framework.LongPolling",
                    "IWF.TeleFlow.Telegram.Framework.Webhooks",
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
                    "IWF.TeleFlow.Core",
                    "IWF.TeleFlow.Telegram",
                    "IWF.TeleFlow.Telegram.Framework",
                    "IWF.TeleFlow.Telegram.Framework.LongPolling",
                    "IWF.TeleFlow.Telegram.Framework.Webhooks",
                    "IWF.TeleFlow.Telegram.LongPolling"
                ]),

            new PackageConsumerScenario(
                Name: "FrameworkLongPollingPackageConsumer",
                PackageId: "IWF.TeleFlow.Telegram.Framework.LongPolling",
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
                    "IWF.TeleFlow.Core",
                    "IWF.TeleFlow.Telegram.Client",
                    "IWF.TeleFlow.Telegram.Framework",
                    "IWF.TeleFlow.Telegram.Framework.LongPolling",
                    "IWF.TeleFlow.Telegram.LongPolling",
                    "IWF.TeleFlow.Telegram.Schema"
                ],
                ForbiddenPackages:
                [
                    "IWF.TeleFlow.Telegram",
                    "IWF.TeleFlow.Telegram.Framework.Webhooks",
                    "IWF.TeleFlow.Telegram.Webhooks"
                ]),

            new PackageConsumerScenario(
                Name: "FrameworkWebhooksPackageConsumer",
                PackageId: "IWF.TeleFlow.Telegram.Framework.Webhooks",
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
                    "IWF.TeleFlow.Core",
                    "IWF.TeleFlow.Telegram.Client",
                    "IWF.TeleFlow.Telegram.Framework",
                    "IWF.TeleFlow.Telegram.Framework.Webhooks",
                    "IWF.TeleFlow.Telegram.Schema",
                    "IWF.TeleFlow.Telegram.Webhooks"
                ],
                ForbiddenPackages:
                [
                    "IWF.TeleFlow.Telegram",
                    "IWF.TeleFlow.Telegram.Framework.LongPolling",
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
                    "IWF.TeleFlow.Core",
                    "IWF.TeleFlow.Telegram.Framework",
                    "IWF.TeleFlow.Telegram.Framework.LongPolling",
                    "IWF.TeleFlow.Telegram.Framework.Webhooks",
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

    private sealed record PackageProject(string PackageId, string RelativeProjectPath);

    private sealed record PackageIdentity(
        string Id,
        string Version,
        string Description,
        string Authors,
        string Tags,
        string Readme);
}
