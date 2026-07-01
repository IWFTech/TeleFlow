namespace TeleFlow.Framework.States;

/// <summary>
/// Builds deterministic string keys from <see cref="StateKey"/> for durable state providers while
/// preserving the structured state ownership model used by the runtime pipeline.
/// </summary>
public sealed class DefaultStateStorageKeyBuilder : IStateStorageKeyBuilder
{
    public string Build(StateKey key, StateStorageKeyPart part)
    {
        return string.Join(
            ":",
            Escape(key.Namespace),
            FormatPart(part),
            $"scope={Escape(key.Scope)}",
            $"subject={Escape(key.Subject)}",
            $"partition={Escape(key.Partition)}",
            $"destiny={Escape(key.Destiny)}");
    }

    private static string FormatPart(StateStorageKeyPart part)
    {
        return part switch
        {
            StateStorageKeyPart.State => "state",
            StateStorageKeyPart.Data => "data",
            StateStorageKeyPart.History => "history",
            StateStorageKeyPart.Lock => "lock",
            _ => throw new ArgumentOutOfRangeException(nameof(part), part, "Unknown state storage key part.")
        };
    }

    private static string Escape(string? value)
    {
        return value is null
            ? string.Empty
            : Uri.EscapeDataString(value);
    }
}
