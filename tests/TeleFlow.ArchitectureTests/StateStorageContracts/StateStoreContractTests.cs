using TeleFlow.Core.States;

namespace TeleFlow.ArchitectureTests.StateStorageContracts;

public abstract class StateStoreContractTests
{
    protected abstract IStateStore CreateStore();

    [Fact]
    public async Task SetGetOverwriteClear_AndIsolateKeys()
    {
        var store = CreateStore();
        var firstKey = StateKey.Create("telegram", "user:1", "chat:10");
        var secondKey = StateKey.Create("telegram", "user:1", "chat:20");

        await store.SetStateAsync(firstKey, "first");
        await store.SetStateAsync(secondKey, "second");

        Assert.Equal("first", await store.GetStateAsync(firstKey));
        Assert.Equal("second", await store.GetStateAsync(secondKey));

        await store.SetStateAsync(firstKey, "first:updated");

        Assert.Equal("first:updated", await store.GetStateAsync(firstKey));
        Assert.Equal("second", await store.GetStateAsync(secondKey));

        await store.ClearStateAsync(firstKey);

        Assert.Null(await store.GetStateAsync(firstKey));
        Assert.Equal("second", await store.GetStateAsync(secondKey));
    }

    [Fact]
    public async Task ClearMissingState_IsNoOp()
    {
        var store = CreateStore();
        var key = StateKey.Create("telegram", "user:1", "chat:10");

        await store.ClearStateAsync(key);

        Assert.Null(await store.GetStateAsync(key));
    }

    [Fact]
    public async Task SetStateAsync_RejectsEmptyState()
    {
        var store = CreateStore();
        var key = StateKey.Create("telegram", "user:1", "chat:10");

        await Assert.ThrowsAsync<ArgumentException>(async () => await store.SetStateAsync(key, " "));
    }

    [Fact]
    public async Task Operations_RespectPreCanceledToken()
    {
        var store = CreateStore();
        var key = StateKey.Create("telegram", "user:1", "chat:10");
        using var cts = new CancellationTokenSource();

        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.GetStateAsync(key, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.SetStateAsync(key, "state", cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.ClearStateAsync(key, cts.Token));
    }
}
