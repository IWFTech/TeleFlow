using TeleFlow.Core.States;

namespace TeleFlow.ArchitectureTests.StateStorageContracts;

public abstract class StateDataStoreContractTests
{
    protected abstract IStateDataStore CreateStore();

    [Fact]
    public async Task SetGetRemoveClear_AndIsolateKeys()
    {
        var store = CreateStore();
        var firstKey = StateKey.Create("telegram", "user:1", "chat:10");
        var secondKey = StateKey.Create("telegram", "user:1", "chat:20");

        await store.SetDataAsync(firstKey, "name", "\"Alice\"");
        await store.SetDataAsync(firstKey, "age", "42");
        await store.SetDataAsync(firstKey, "empty", string.Empty);
        await store.SetDataAsync(secondKey, "name", "\"Bob\"");

        Assert.Equal("\"Alice\"", await store.GetDataAsync(firstKey, "name"));
        Assert.Equal("42", await store.GetDataAsync(firstKey, "age"));
        Assert.Equal(string.Empty, await store.GetDataAsync(firstKey, "empty"));
        Assert.Equal("\"Bob\"", await store.GetDataAsync(secondKey, "name"));

        await store.SetDataAsync(firstKey, "name", "\"Ann\"");

        Assert.Equal("\"Ann\"", await store.GetDataAsync(firstKey, "name"));

        await store.RemoveDataAsync(firstKey, "name");

        Assert.Null(await store.GetDataAsync(firstKey, "name"));
        Assert.Equal("42", await store.GetDataAsync(firstKey, "age"));
        Assert.Equal("\"Bob\"", await store.GetDataAsync(secondKey, "name"));

        await store.ClearDataAsync(firstKey);

        Assert.Null(await store.GetDataAsync(firstKey, "age"));
        Assert.Null(await store.GetDataAsync(firstKey, "empty"));
        Assert.Equal("\"Bob\"", await store.GetDataAsync(secondKey, "name"));
    }

    [Fact]
    public async Task RemoveAndClearMissingData_AreNoOps()
    {
        var store = CreateStore();
        var key = StateKey.Create("telegram", "user:1", "chat:10");

        await store.RemoveDataAsync(key, "missing");
        await store.ClearDataAsync(key);

        Assert.Null(await store.GetDataAsync(key, "missing"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task DataKeyOperations_RejectInvalidDataKeys(string dataKey)
    {
        var store = CreateStore();
        var key = StateKey.Create("telegram", "user:1", "chat:10");

        await Assert.ThrowsAsync<ArgumentException>(async () => await store.GetDataAsync(key, dataKey));
        await Assert.ThrowsAsync<ArgumentException>(async () => await store.SetDataAsync(key, dataKey, "value"));
        await Assert.ThrowsAsync<ArgumentException>(async () => await store.RemoveDataAsync(key, dataKey));
    }

    [Fact]
    public async Task SetDataAsync_RejectsNullValue()
    {
        var store = CreateStore();
        var key = StateKey.Create("telegram", "user:1", "chat:10");

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await store.SetDataAsync(key, "name", null!));
    }

    [Fact]
    public async Task Operations_RespectPreCanceledToken()
    {
        var store = CreateStore();
        var key = StateKey.Create("telegram", "user:1", "chat:10");
        using var cts = new CancellationTokenSource();

        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.GetDataAsync(key, "name", cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.SetDataAsync(key, "name", "value", cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.RemoveDataAsync(key, "name", cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.ClearDataAsync(key, cts.Token));
    }
}
