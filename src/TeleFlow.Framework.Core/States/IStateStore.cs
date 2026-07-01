namespace TeleFlow.Framework.States;

public interface IStateStore
{
    ValueTask<string?> GetStateAsync(StateKey key, CancellationToken cancellationToken = default);

    ValueTask SetStateAsync(StateKey key, string state, CancellationToken cancellationToken = default);

    ValueTask ClearStateAsync(StateKey key, CancellationToken cancellationToken = default);
}
