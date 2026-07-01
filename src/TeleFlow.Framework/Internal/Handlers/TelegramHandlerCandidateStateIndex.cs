namespace TeleFlow.Telegram.Internal.Handlers;

/// <summary>
/// Builds immutable state-indexed handler candidate buckets used by message, command, chat-member, and
/// callback route plans during Telegram update selection.
/// </summary>
internal static class TelegramHandlerCandidateStateIndex
{
    public static readonly IReadOnlyList<TelegramHandlerCandidate> EmptyCandidates = [];

    public static void Add(
        Dictionary<string, List<TelegramHandlerCandidate>> stateful,
        IReadOnlyList<string> states,
        TelegramHandlerCandidate candidate)
    {
        for (var index = 0; index < states.Count; index++)
        {
            var state = states[index];

            if (!stateful.TryGetValue(state, out var candidates))
            {
                candidates = [];
                stateful.Add(state, candidates);
            }

            candidates.Add(candidate);
        }
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<TelegramHandlerCandidate>> Freeze(
        Dictionary<string, List<TelegramHandlerCandidate>> stateful)
    {
        var frozen = new Dictionary<string, IReadOnlyList<TelegramHandlerCandidate>>(
            stateful.Count,
            StringComparer.Ordinal);

        foreach (var item in stateful)
        {
            frozen.Add(item.Key, item.Value.ToArray());
        }

        return frozen;
    }
}
