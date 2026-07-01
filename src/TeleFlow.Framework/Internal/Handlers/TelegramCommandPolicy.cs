namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed class TelegramCommandPolicy
{
    public static TelegramCommandPolicy Default { get; } = new(["/"], allowSpaceAfterPrefix: false, ignoreCase: true);

    public TelegramCommandPolicy(
        IReadOnlyList<string> prefixes,
        bool allowSpaceAfterPrefix,
        bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(prefixes);

        if (prefixes.Count == 0 || prefixes.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Telegram command route prefixes must contain at least one non-empty prefix.");
        }

        Prefixes = prefixes.Select(static prefix => prefix.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        AllowSpaceAfterPrefix = allowSpaceAfterPrefix;
        IgnoreCase = ignoreCase;
    }

    public IReadOnlyList<string> Prefixes { get; }

    public bool AllowSpaceAfterPrefix { get; }

    public bool IgnoreCase { get; }
}
