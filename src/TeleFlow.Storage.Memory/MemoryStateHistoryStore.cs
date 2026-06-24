using TeleFlow.Core.States;

namespace TeleFlow.Storage.Memory;

public sealed class MemoryStateHistoryStore : IStateHistoryStore
{
    private readonly object _gate = new();
    private readonly Dictionary<StateKey, List<string>> _history = [];

    public ValueTask<IReadOnlyList<string>> GetHistoryAsync(
        StateKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_history.TryGetValue(key, out var history))
            {
                return ValueTask.FromResult<IReadOnlyList<string>>([]);
            }

            return ValueTask.FromResult<IReadOnlyList<string>>(history.ToArray());
        }
    }

    public ValueTask PushAsync(
        StateKey key,
        string stateId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);

        lock (_gate)
        {
            if (!_history.TryGetValue(key, out var history))
            {
                history = [];
                _history.Add(key, history);
            }

            history.Add(stateId);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> PopAsync(
        StateKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!TryPeek(key, out var history, out var state))
            {
                return ValueTask.FromResult<string?>(null);
            }

            history.RemoveAt(history.Count - 1);

            if (history.Count == 0)
            {
                _history.Remove(key);
            }

            return ValueTask.FromResult<string?>(state);
        }
    }

    public ValueTask<string?> PeekAsync(
        StateKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!TryPeek(key, out _, out var state))
            {
                return ValueTask.FromResult<string?>(null);
            }

            return ValueTask.FromResult<string?>(state);
        }
    }

    public ValueTask ClearAsync(
        StateKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _history.Remove(key);
        }

        return ValueTask.CompletedTask;
    }

    private bool TryPeek(
        StateKey key,
        out List<string> history,
        out string? state)
    {
        if (!_history.TryGetValue(key, out history!) ||
            history.Count == 0)
        {
            state = null;
            return false;
        }

        state = history[^1];
        return true;
    }
}
