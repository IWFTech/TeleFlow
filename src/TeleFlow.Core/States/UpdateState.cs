namespace TeleFlow.Core.States;

public sealed class UpdateState
{
    private readonly IStateStore _stateStore;
    private readonly IStateDataStore? _dataStore;
    private readonly IStateDataSerializer? _dataSerializer;
    private readonly IStateHistoryStore? _historyStore;
    private UpdateStateData? _data;
    private UpdateWizard? _wizard;
    private string? _currentState;
    private bool _isHydrated;

    public UpdateState(
        IStateStore stateStore,
        StateKey key,
        IStateDataStore? dataStore = null,
        IStateDataSerializer? dataSerializer = null,
        IStateHistoryStore? historyStore = null)
    {
        ArgumentNullException.ThrowIfNull(stateStore);

        _stateStore = stateStore;
        _dataStore = dataStore;
        _dataSerializer = dataSerializer;
        _historyStore = historyStore;
        Key = key;
    }

    public StateKey Key { get; }

    internal string? CurrentStateSnapshot
    {
        get
        {
            if (!_isHydrated)
            {
                throw new InvalidOperationException(
                    "State snapshot is not hydrated for the current update. Call GetAsync, SetAsync, ClearAsync, Wizard.GetCurrentAsync, or Wizard.GoToAsync before reading Wizard.Current.");
            }

            return _currentState;
        }
    }

    public UpdateStateData Data
    {
        get
        {
            if (_data is not null)
            {
                return _data;
            }

            if (_dataStore is null || _dataSerializer is null)
            {
                throw new InvalidOperationException(
                    "State data is not available for the current update. Register state data storage and serializer before using ctx.State.Data.");
            }

            _data = new UpdateStateData(_dataStore, _dataSerializer, Key);
            return _data;
        }
    }

    public UpdateWizard Wizard
    {
        get
        {
            if (_wizard is not null)
            {
                return _wizard;
            }

            if (_historyStore is null)
            {
                throw new InvalidOperationException(
                    "Wizard is not available for the current update. Register state history storage before using ctx.State.Wizard.");
            }

            _wizard = new UpdateWizard(this, _historyStore);
            return _wizard;
        }
    }

    public async ValueTask<string?> GetAsync(CancellationToken cancellationToken = default)
    {
        await EnsureHydratedAsync(cancellationToken).ConfigureAwait(false);
        return _currentState;
    }

    public async ValueTask SetAsync(string state, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        await _stateStore.SetStateAsync(Key, state, cancellationToken).ConfigureAwait(false);
        _currentState = state;
        _isHydrated = true;
    }

    public ValueTask SetAsync(State state, CancellationToken cancellationToken = default)
    {
        return SetAsync(state.Id, cancellationToken);
    }

    public ValueTask<bool> IsAsync(State state, CancellationToken cancellationToken = default)
    {
        if (_isHydrated)
        {
            return ValueTask.FromResult(string.Equals(_currentState, state.Id, StringComparison.Ordinal));
        }

        return IsAsyncSlowAsync(state, cancellationToken);
    }

    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        await _stateStore.ClearStateAsync(Key, cancellationToken).ConfigureAwait(false);
        _currentState = null;
        _isHydrated = true;
    }

    public async ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        await Data.ClearAsync(cancellationToken).ConfigureAwait(false);
        await ClearAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EnsureHydratedAsync(CancellationToken cancellationToken)
    {
        if (_isHydrated)
        {
            return;
        }

        _currentState = await _stateStore.GetStateAsync(Key, cancellationToken).ConfigureAwait(false);
        _isHydrated = true;
    }

    private async ValueTask<bool> IsAsyncSlowAsync(State state, CancellationToken cancellationToken)
    {
        await EnsureHydratedAsync(cancellationToken).ConfigureAwait(false);
        return string.Equals(_currentState, state.Id, StringComparison.Ordinal);
    }
}
