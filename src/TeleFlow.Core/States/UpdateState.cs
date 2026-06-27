namespace TeleFlow.Core.States;

public sealed class UpdateState
{
    private readonly IStateStore _stateStore;
    private readonly UpdateStateData? _data;
    private readonly UpdateWizard? _wizard;
    private string? _currentState;
    private bool _currentStateCached;

    public UpdateState(
        IStateStore stateStore,
        StateKey key,
        IStateDataStore? dataStore = null,
        IStateDataSerializer? dataSerializer = null,
        IStateHistoryStore? historyStore = null)
    {
        ArgumentNullException.ThrowIfNull(stateStore);

        _stateStore = stateStore;
        Key = key;

        if (dataStore is not null && dataSerializer is not null)
        {
            _data = new UpdateStateData(dataStore, dataSerializer, key);
        }

        if (historyStore is not null)
        {
            _wizard = new UpdateWizard(this, historyStore);
        }
    }

    public StateKey Key { get; }

    internal string? CurrentStateSnapshot => _currentState;

    public UpdateStateData Data => _data ??
        throw new InvalidOperationException(
            "State data is not available for the current update. Register state data storage and serializer before using ctx.State.Data.");

    public UpdateWizard Wizard => _wizard ??
        throw new InvalidOperationException(
            "Wizard is not available for the current update. Register state history storage before using ctx.State.Wizard.");

    public async ValueTask<string?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_currentStateCached)
        {
            return _currentState;
        }

        _currentState = await _stateStore.GetStateAsync(Key, cancellationToken).ConfigureAwait(false);
        _currentStateCached = true;
        return _currentState;
    }

    public async ValueTask SetAsync(string state, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        await _stateStore.SetStateAsync(Key, state, cancellationToken).ConfigureAwait(false);
        _currentState = state;
        _currentStateCached = true;
    }

    public ValueTask SetAsync(State state, CancellationToken cancellationToken = default)
    {
        return SetAsync(state.Id, cancellationToken);
    }

    public async ValueTask<bool> IsAsync(State state, CancellationToken cancellationToken = default)
    {
        var currentState = await GetAsync(cancellationToken).ConfigureAwait(false);
        return string.Equals(currentState, state.Id, StringComparison.Ordinal);
    }

    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        await _stateStore.ClearStateAsync(Key, cancellationToken).ConfigureAwait(false);
        _currentState = null;
        _currentStateCached = true;
    }

    public async ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        await Data.ClearAsync(cancellationToken).ConfigureAwait(false);
        await ClearAsync(cancellationToken).ConfigureAwait(false);
    }
}
