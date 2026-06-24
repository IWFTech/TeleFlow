namespace TeleFlow.Core.States;

public sealed class UpdateWizard
{
    private readonly UpdateState _state;
    private readonly IStateHistoryStore _historyStore;

    internal UpdateWizard(
        UpdateState state,
        IStateHistoryStore historyStore)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(historyStore);

        _state = state;
        _historyStore = historyStore;
    }

    public State Current
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_state.CurrentStateSnapshot))
            {
                throw new InvalidOperationException(
                    "Wizard current state is not available because the current update has no active state.");
            }

            return State.Create(_state.CurrentStateSnapshot);
        }
    }

    public UpdateStateData Data => _state.Data;

    public async ValueTask<State?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var currentState = await _state.GetAsync(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(currentState)
            ? null
            : State.Create(currentState);
    }

    public async ValueTask GoToAsync(
        State state,
        CancellationToken cancellationToken = default)
    {
        var currentState = await _state.GetAsync(cancellationToken).ConfigureAwait(false);
        await _state.SetAsync(state, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(currentState))
        {
            await _historyStore.PushAsync(_state.Key, currentState, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask BackAsync(CancellationToken cancellationToken = default)
    {
        var previousState = await _historyStore.PeekAsync(_state.Key, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(previousState))
        {
            throw new InvalidOperationException(
                "Wizard cannot go back because state history is empty for the current update.");
        }

        await _state.SetAsync(previousState, cancellationToken).ConfigureAwait(false);

        var poppedState = await _historyStore.PopAsync(_state.Key, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(poppedState, previousState, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Wizard state history changed while going back. State history storage returned an unexpected previous state.");
        }
    }

    public async ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        await _state.Data.ClearAsync(cancellationToken).ConfigureAwait(false);
        await _historyStore.ClearAsync(_state.Key, cancellationToken).ConfigureAwait(false);
        await _state.ClearAsync(cancellationToken).ConfigureAwait(false);
    }
}
