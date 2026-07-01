namespace TeleFlow.Framework.States;

public sealed class UpdateStateData
{
    private readonly IStateDataStore _store;
    private readonly IStateDataSerializer _serializer;

    public UpdateStateData(
        IStateDataStore store,
        IStateDataSerializer serializer,
        StateKey key)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(serializer);

        _store = store;
        _serializer = serializer;
        Key = key;
    }

    public StateKey Key { get; }

    public async ValueTask<TValue?> GetAsync<TValue>(
        string key,
        CancellationToken cancellationToken = default)
    {
        ValidateDataKey(key);

        var value = await _store.GetDataAsync(Key, key, cancellationToken).ConfigureAwait(false);
        return value is null
            ? default
            : _serializer.Deserialize<TValue>(value);
    }

    public async ValueTask<TValue> GetRequiredAsync<TValue>(
        string key,
        CancellationToken cancellationToken = default)
    {
        ValidateDataKey(key);

        var value = await _store.GetDataAsync(Key, key, cancellationToken).ConfigureAwait(false);
        if (value is null)
        {
            throw new KeyNotFoundException($"State data key '{key}' was not found.");
        }

        var result = _serializer.Deserialize<TValue>(value);
        if (result is null)
        {
            throw new InvalidOperationException($"State data key '{key}' deserialized to null.");
        }

        return result;
    }

    public ValueTask SetAsync<TValue>(
        string key,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        ValidateDataKey(key);
        ArgumentNullException.ThrowIfNull(value);

        var serialized = _serializer.Serialize(value);
        return _store.SetDataAsync(Key, key, serialized, cancellationToken);
    }

    public ValueTask RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ValidateDataKey(key);
        return _store.RemoveDataAsync(Key, key, cancellationToken);
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        return _store.ClearDataAsync(Key, cancellationToken);
    }

    private static void ValidateDataKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
    }
}
