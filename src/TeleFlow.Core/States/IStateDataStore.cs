namespace TeleFlow.Core.States;

public interface IStateDataStore
{
    ValueTask<string?> GetDataAsync(
        StateKey key,
        string dataKey,
        CancellationToken cancellationToken = default);

    ValueTask SetDataAsync(
        StateKey key,
        string dataKey,
        string value,
        CancellationToken cancellationToken = default);

    ValueTask RemoveDataAsync(
        StateKey key,
        string dataKey,
        CancellationToken cancellationToken = default);

    ValueTask ClearDataAsync(
        StateKey key,
        CancellationToken cancellationToken = default);
}
