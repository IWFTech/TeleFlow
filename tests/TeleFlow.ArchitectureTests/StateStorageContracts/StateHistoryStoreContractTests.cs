using TeleFlow.Framework.States;

namespace TeleFlow.ArchitectureTests.StateStorageContracts;

public abstract class StateHistoryStoreContractTests
{
    protected abstract IStateHistoryStore CreateStore();

    [Fact]
    public async Task PushGetPeekPopClear_AndIsolateKeys()
    {
        var store = CreateStore();
        var firstKey = StateKey.Create("telegram", "user:1", "chat:10");
        var secondKey = StateKey.Create("telegram", "user:1", "chat:20");

        await store.PushAsync(firstKey, "first:name");
        await store.PushAsync(firstKey, "first:age");
        await store.PushAsync(secondKey, "second:name");

        Assert.Equal(["first:name", "first:age"], await store.GetHistoryAsync(firstKey));
        Assert.Equal(["second:name"], await store.GetHistoryAsync(secondKey));
        Assert.Equal("first:age", await store.PeekAsync(firstKey));
        Assert.Equal(["first:name", "first:age"], await store.GetHistoryAsync(firstKey));
        Assert.Equal("first:age", await store.PopAsync(firstKey));
        Assert.Equal("first:name", await store.PeekAsync(firstKey));
        Assert.Equal("first:name", await store.PopAsync(firstKey));
        Assert.Null(await store.PeekAsync(firstKey));
        Assert.Null(await store.PopAsync(firstKey));
        Assert.Equal(["second:name"], await store.GetHistoryAsync(secondKey));

        await store.ClearAsync(secondKey);

        Assert.Empty(await store.GetHistoryAsync(secondKey));
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsSnapshotNotLiveStorage()
    {
        var store = CreateStore();
        var key = StateKey.Create("telegram", "user:1", "chat:10");

        await store.PushAsync(key, "first");

        var history = await store.GetHistoryAsync(key);

        await store.PushAsync(key, "second");

        Assert.Equal(["first"], history);
        Assert.Equal(["first", "second"], await store.GetHistoryAsync(key));
    }

    [Fact]
    public async Task PopPeekAndClearMissingHistory_AreNoOps()
    {
        var store = CreateStore();
        var key = StateKey.Create("telegram", "user:1", "chat:10");

        Assert.Null(await store.PeekAsync(key));
        Assert.Null(await store.PopAsync(key));

        await store.ClearAsync(key);

        Assert.Empty(await store.GetHistoryAsync(key));
    }

    [Fact]
    public async Task PushAsync_RejectsEmptyState()
    {
        var store = CreateStore();
        var key = StateKey.Create("telegram", "user:1", "chat:10");

        await Assert.ThrowsAsync<ArgumentException>(async () => await store.PushAsync(key, " "));
    }

    [Fact]
    public async Task Operations_RespectPreCanceledToken()
    {
        var store = CreateStore();
        var key = StateKey.Create("telegram", "user:1", "chat:10");
        using var cts = new CancellationTokenSource();

        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.GetHistoryAsync(key, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.PushAsync(key, "state", cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.PeekAsync(key, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.PopAsync(key, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.ClearAsync(key, cts.Token));
    }
}
