using TeleFlow.Annotations;

namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed class TelegramCommandPolicy
{
    public static TelegramCommandPolicy Default { get; } = new(["/"], allowSpaceAfterPrefix: false, ignoreCase: true);

    public TelegramCommandPolicy(
        IReadOnlyList<string> prefixes,
        bool allowSpaceAfterPrefix,
        bool ignoreCase,
        CommandPrefixMode prefixMode = CommandPrefixMode.Required)
    {
        ArgumentNullException.ThrowIfNull(prefixes);

        if (!Enum.IsDefined(prefixMode))
        {
            throw new InvalidOperationException($"Telegram command prefix mode '{prefixMode}' is not supported.");
        }

        if (prefixes.Count == 0 || prefixes.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Telegram command route prefixes must contain at least one non-empty prefix.");
        }

        Prefixes = prefixes
            .Select(static prefix => prefix.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(static prefix => prefix.Length)
            .ThenBy(static prefix => prefix, StringComparer.Ordinal)
            .ToArray();
        AllowSpaceAfterPrefix = allowSpaceAfterPrefix;
        IgnoreCase = ignoreCase;
        PrefixMode = prefixMode;
    }

    public IReadOnlyList<string> Prefixes { get; }

    public bool AllowSpaceAfterPrefix { get; }

    public bool IgnoreCase { get; }

    public CommandPrefixMode PrefixMode { get; }
}
