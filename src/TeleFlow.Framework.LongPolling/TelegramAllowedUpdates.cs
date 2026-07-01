namespace TeleFlow.Telegram;

public sealed class TelegramAllowedUpdates
{
    private TelegramAllowedUpdates(TelegramAllowedUpdatesMode mode, IReadOnlyList<TelegramUpdateType> updateTypes)
    {
        Mode = mode;
        UpdateTypes = updateTypes;
    }

    internal TelegramAllowedUpdatesMode Mode { get; }

    internal IReadOnlyList<TelegramUpdateType> UpdateTypes { get; }

    public static TelegramAllowedUpdates Auto { get; } = new(TelegramAllowedUpdatesMode.Auto, []);

    public static TelegramAllowedUpdates All { get; } = new(TelegramAllowedUpdatesMode.All, []);

    public static TelegramAllowedUpdates Only(params TelegramUpdateType[] updateTypes)
    {
        return Only((IEnumerable<TelegramUpdateType>)updateTypes);
    }

    public static TelegramAllowedUpdates Only(IEnumerable<TelegramUpdateType> updateTypes)
    {
        ArgumentNullException.ThrowIfNull(updateTypes);

        var normalized = updateTypes.ToArray();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("At least one Telegram update type must be specified.", nameof(updateTypes));
        }

        foreach (var updateType in normalized)
        {
            if (string.IsNullOrWhiteSpace(updateType.Value))
            {
                throw new ArgumentException("Telegram update type values must not be empty.", nameof(updateTypes));
            }
        }

        var duplicate = normalized
            .GroupBy(static updateType => updateType.Value, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Duplicate Telegram update type '{duplicate.Key}' was specified.",
                nameof(updateTypes));
        }

        return new TelegramAllowedUpdates(TelegramAllowedUpdatesMode.Only, normalized);
    }
}

internal enum TelegramAllowedUpdatesMode
{
    Auto,
    All,
    Only
}
