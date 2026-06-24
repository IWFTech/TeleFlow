using System.Collections.Concurrent;
using TeleFlow.Core.States;

namespace TeleFlow.Storage.Memory;

public sealed class MemoryStateDataStore : IStateDataStore
{
    private readonly ConcurrentDictionary<StateDataKey, string> _data = new();

    public ValueTask<string?> GetDataAsync(
        StateKey key,
        string dataKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateDataKey(dataKey);

        _data.TryGetValue(new StateDataKey(key, dataKey), out var value);
        return ValueTask.FromResult(value);
    }

    public ValueTask SetDataAsync(
        StateKey key,
        string dataKey,
        string value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateDataKey(dataKey);
        ArgumentNullException.ThrowIfNull(value);

        _data[new StateDataKey(key, dataKey)] = value;
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveDataAsync(
        StateKey key,
        string dataKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateDataKey(dataKey);

        _data.TryRemove(new StateDataKey(key, dataKey), out _);
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearDataAsync(
        StateKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var candidate in _data.Keys)
        {
            if (candidate.StateKey == key)
            {
                _data.TryRemove(candidate, out _);
            }
        }

        return ValueTask.CompletedTask;
    }

    private static void ValidateDataKey(string dataKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataKey);
    }

    private readonly record struct StateDataKey(StateKey StateKey, string DataKey);
}
