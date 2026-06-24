using System.Xml.Linq;

namespace TeleFlow.ArchitectureTests;

public sealed class ProjectGraphTests
{
    private static readonly string RepositoryRoot = ResolveRepositoryRoot();

    [Fact]
    public void Solution_ContainsExpectedStageOneProjects()
    {
        var solutionText = File.ReadAllText(Path.Combine(RepositoryRoot, "TeleFlow.sln"));

        Assert.Contains("TeleFlow.Core", solutionText);
        Assert.Contains("TeleFlow.Telegram", solutionText);
        Assert.Contains("TeleFlow.Telegram.Client", solutionText);
        Assert.Contains("TeleFlow.Telegram.Framework", solutionText);
        Assert.Contains("TeleFlow.Telegram.Framework.LongPolling", solutionText);
        Assert.Contains("TeleFlow.Telegram.Framework.Webhooks", solutionText);
        Assert.Contains("TeleFlow.Telegram.LongPolling", solutionText);
        Assert.Contains("TeleFlow.Telegram.Schema", solutionText);
        Assert.Contains("TeleFlow.Annotations", solutionText);
        Assert.Contains("TeleFlow.Generators", solutionText);
        Assert.Contains("TeleFlow.Storage.Memory", solutionText);
        Assert.Contains("TeleFlow.Telegram.Webhooks", solutionText);
        Assert.DoesNotContain("Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"TeleFlow\"", solutionText);
    }

    [Fact]
    public void Core_HasNoProjectReferences()
    {
        Assert.Empty(GetProjectReferences("src/TeleFlow.Core/TeleFlow.Core.csproj"));
    }

    [Fact]
    public void Annotations_HasNoProjectReferences()
    {
        Assert.Empty(GetProjectReferences("src/TeleFlow.Annotations/TeleFlow.Annotations.csproj"));
    }

    [Fact]
    public void Schema_HasNoProjectReferences()
    {
        Assert.Empty(GetProjectReferences("src/TeleFlow.Telegram.Schema/TeleFlow.Telegram.Schema.csproj"));
    }

    [Fact]
    public void Telegram_ReferencesClientOnly()
    {
        Assert.Equal(
            ["..\\TeleFlow.Telegram.Client\\TeleFlow.Telegram.Client.csproj"],
            GetProjectReferences("src/TeleFlow.Telegram/TeleFlow.Telegram.csproj"));
    }

    [Fact]
    public void TelegramClient_ReferencesSchemaOnly()
    {
        Assert.Equal(
            ["..\\TeleFlow.Telegram.Schema\\TeleFlow.Telegram.Schema.csproj"],
            GetProjectReferences("src/TeleFlow.Telegram.Client/TeleFlow.Telegram.Client.csproj"));
    }

    [Fact]
    public void TelegramFramework_ReferencesClientSchemaCoreAndAnnotationsOnly()
    {
        Assert.Equal(
            [
                "..\\TeleFlow.Annotations\\TeleFlow.Annotations.csproj",
                "..\\TeleFlow.Core\\TeleFlow.Core.csproj",
                "..\\TeleFlow.Telegram.Client\\TeleFlow.Telegram.Client.csproj",
                "..\\TeleFlow.Telegram.Schema\\TeleFlow.Telegram.Schema.csproj"
            ],
            GetProjectReferences("src/TeleFlow.Telegram.Framework/TeleFlow.Telegram.Framework.csproj"));
    }

    [Fact]
    public void TelegramLongPolling_ReferencesClientAndSchemaOnly()
    {
        Assert.Equal(
            [
                "..\\TeleFlow.Telegram.Client\\TeleFlow.Telegram.Client.csproj",
                "..\\TeleFlow.Telegram.Schema\\TeleFlow.Telegram.Schema.csproj"
            ],
            GetProjectReferences("src/TeleFlow.Telegram.LongPolling/TeleFlow.Telegram.LongPolling.csproj"));
    }

    [Fact]
    public void TelegramFrameworkLongPolling_ReferencesCoreFrameworkAndLongPollingOnly()
    {
        Assert.Equal(
            [
                "..\\TeleFlow.Core\\TeleFlow.Core.csproj",
                "..\\TeleFlow.Telegram.Framework\\TeleFlow.Telegram.Framework.csproj",
                "..\\TeleFlow.Telegram.LongPolling\\TeleFlow.Telegram.LongPolling.csproj"
            ],
            GetProjectReferences("src/TeleFlow.Telegram.Framework.LongPolling/TeleFlow.Telegram.Framework.LongPolling.csproj"));
    }

    [Fact]
    public void TelegramFrameworkWebhooks_ReferencesCoreFrameworkAndWebhooksOnly()
    {
        Assert.Equal(
            [
                "..\\TeleFlow.Core\\TeleFlow.Core.csproj",
                "..\\TeleFlow.Telegram.Framework\\TeleFlow.Telegram.Framework.csproj",
                "..\\TeleFlow.Telegram.Webhooks\\TeleFlow.Telegram.Webhooks.csproj"
            ],
            GetProjectReferences("src/TeleFlow.Telegram.Framework.Webhooks/TeleFlow.Telegram.Framework.Webhooks.csproj"));
    }

    [Fact]
    public void TelegramWebhooks_ReferencesClientAndSchemaOnly()
    {
        Assert.Equal(
            [
                "..\\TeleFlow.Telegram.Client\\TeleFlow.Telegram.Client.csproj",
                "..\\TeleFlow.Telegram.Schema\\TeleFlow.Telegram.Schema.csproj"
            ],
            GetProjectReferences("src/TeleFlow.Telegram.Webhooks/TeleFlow.Telegram.Webhooks.csproj"));
    }

    [Fact]
    public void StorageMemory_ReferencesCoreOnly()
    {
        Assert.Equal(
            ["..\\TeleFlow.Core\\TeleFlow.Core.csproj"],
            GetProjectReferences("src/TeleFlow.Storage.Memory/TeleFlow.Storage.Memory.csproj"));
    }

    [Fact]
    public void Generators_HasNoProjectReferences()
    {
        Assert.Empty(GetProjectReferences("src/TeleFlow.Generators/TeleFlow.Generators.csproj"));
    }

    [Fact]
    public void Generators_TargetsNetstandardForAnalyzerCompatibilityOnly()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot, "src", "TeleFlow.Generators", "TeleFlow.Generators.csproj"));
        var targetFramework = document
            .Descendants("TargetFramework")
            .Single()
            .Value;

        Assert.Equal("netstandard2.0", targetFramework);
    }

    private static IReadOnlyList<string> GetProjectReferences(string relativeProjectPath)
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot, relativeProjectPath));

        return document
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
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
}
