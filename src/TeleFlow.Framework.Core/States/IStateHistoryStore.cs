namespace TeleFlow.Framework.States;

public interface IStateHistoryStore
{
    ValueTask<IReadOnlyList<string>> GetHistoryAsync(
        StateKey key,
        CancellationToken cancellationToken = default);

    ValueTask PushAsync(
        StateKey key,
        string stateId,
        CancellationToken cancellationToken = default);

    ValueTask<string?> PeekAsync(
        StateKey key,
        CancellationToken cancellationToken = default);

    ValueTask<string?> PopAsync(
        StateKey key,
        CancellationToken cancellationToken = default);

    ValueTask ClearAsync(
        StateKey key,
        CancellationToken cancellationToken = default);
}
