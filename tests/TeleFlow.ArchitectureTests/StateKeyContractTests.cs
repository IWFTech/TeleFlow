using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Framework.DependencyInjection;
using TeleFlow.Framework.States;

namespace TeleFlow.ArchitectureTests;

public sealed class StateKeyContractTests
{
    [Fact]
    public void Create_UsesDefaultNamespaceAndDestinyForExistingThreePartShape()
    {
        var key = StateKey.Create("telegram", "user:5", "chat:100");

        Assert.Equal(StateKeyDefaults.DefaultNamespace, key.Namespace);
        Assert.Equal("telegram", key.Scope);
        Assert.Equal("user:5", key.Subject);
        Assert.Equal("chat:100", key.Partition);
        Assert.Equal(StateKeyDefaults.DefaultDestiny, key.Destiny);
    }

    [Fact]
    public void Constructor_StoresExplicitIsolationDimensions()
    {
        var key = new StateKey(
            "support-desk",
            "telegram",
            "user:5",
            "bot:777:chat:100",
            "quiz");

        Assert.Equal("support-desk", key.Namespace);
        Assert.Equal("telegram", key.Scope);
        Assert.Equal("user:5", key.Subject);
        Assert.Equal("bot:777:chat:100", key.Partition);
        Assert.Equal("quiz", key.Destiny);
    }

    [Theory]
    [InlineData("", "telegram", "user:5", "chat:100", "default")]
    [InlineData(" ", "telegram", "user:5", "chat:100", "default")]
    [InlineData("teleflow", "", "user:5", "chat:100", "default")]
    [InlineData("teleflow", " ", "user:5", "chat:100", "default")]
    [InlineData("teleflow", "telegram", "", "chat:100", "default")]
    [InlineData("teleflow", "telegram", " ", "chat:100", "default")]
    [InlineData("teleflow", "telegram", "user:5", " ", "default")]
    [InlineData("teleflow", "telegram", "user:5", "chat:100", "")]
    [InlineData("teleflow", "telegram", "user:5", "chat:100", " ")]
    public void Constructor_RejectsInvalidIsolationDimensions(
        string keyNamespace,
        string scope,
        string subject,
        string? partition,
        string destiny)
    {
        Assert.Throws<ArgumentException>(() => new StateKey(
            keyNamespace,
            scope,
            subject,
            partition,
            destiny));
    }

    [Fact]
    public void WithExpression_RejectsInvalidIsolationDimensions()
    {
        var key = StateKey.Create("telegram", "user:5", "chat:100");

        Assert.Throws<ArgumentException>(() => key with { Namespace = " " });
        Assert.Throws<ArgumentException>(() => key with { Scope = " " });
        Assert.Throws<ArgumentException>(() => key with { Subject = " " });
        Assert.Throws<ArgumentException>(() => key with { Partition = " " });
        Assert.Throws<ArgumentException>(() => key with { Destiny = " " });
    }

    [Theory]
    [InlineData(StateStorageKeyPart.State, "support-desk:state:scope=telegram:subject=user%3A5:partition=bot%3A777%3Achat%3A100:destiny=quiz")]
    [InlineData(StateStorageKeyPart.Data, "support-desk:data:scope=telegram:subject=user%3A5:partition=bot%3A777%3Achat%3A100:destiny=quiz")]
    [InlineData(StateStorageKeyPart.History, "support-desk:history:scope=telegram:subject=user%3A5:partition=bot%3A777%3Achat%3A100:destiny=quiz")]
    [InlineData(StateStorageKeyPart.Lock, "support-desk:lock:scope=telegram:subject=user%3A5:partition=bot%3A777%3Achat%3A100:destiny=quiz")]
    public void DefaultStateStorageKeyBuilder_BuildsStableEscapedKeys(
        StateStorageKeyPart part,
        string expected)
    {
        var key = new StateKey(
            "support-desk",
            "telegram",
            "user:5",
            "bot:777:chat:100",
            "quiz");
        var builder = new DefaultStateStorageKeyBuilder();

        Assert.Equal(expected, builder.Build(key, part));
    }

    [Fact]
    public void DefaultStateStorageKeyBuilder_PreservesNullPartitionAsCanonicalEmptySegment()
    {
        var key = new StateKey(
            "support-desk",
            "telegram",
            "user:5",
            null,
            StateKeyDefaults.DefaultDestiny);
        var builder = new DefaultStateStorageKeyBuilder();

        Assert.Equal(
            "support-desk:state:scope=telegram:subject=user%3A5:partition=:destiny=default",
            builder.Build(key, StateStorageKeyPart.State));
    }

    [Fact]
    public void StateStorageKeyBuilderPolicy_ReplacesExistingBuilder()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IStateStorageKeyBuilder, DefaultStateStorageKeyBuilder>();
        services.AddStateStorageKeyBuilder<CustomStateStorageKeyBuilder>();

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.IsType<CustomStateStorageKeyBuilder>(
            serviceProvider.GetRequiredService<IStateStorageKeyBuilder>());
    }

    private sealed class CustomStateStorageKeyBuilder : IStateStorageKeyBuilder
    {
        public string Build(StateKey key, StateStorageKeyPart part)
        {
            return $"{key.Namespace}:{part}";
        }
    }
}
