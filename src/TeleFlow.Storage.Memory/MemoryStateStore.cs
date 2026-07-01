using System.Collections.Concurrent;
using TeleFlow.Framework.States;

namespace TeleFlow.Storage.Memory;

public sealed class MemoryStateStore : IStateStore
{
    private readonly ConcurrentDictionary<StateKey, string> _states = new();

    public ValueTask<string?> GetStateAsync(
        StateKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _states.TryGetValue(key, out var state);
        return ValueTask.FromResult(state);
    }

    public ValueTask SetStateAsync(
        StateKey key,
        string state,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        _states[key] = state;
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearStateAsync(
        StateKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _states.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }
}
