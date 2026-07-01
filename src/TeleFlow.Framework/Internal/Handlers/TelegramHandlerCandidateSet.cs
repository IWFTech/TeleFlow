namespace TeleFlow.Telegram.Internal.Handlers;

/// <summary>
/// Groups command, message, or chat-member handler candidates by state at handler-table construction time so
/// the selector can skip unrelated stateful handlers during incoming Telegram update dispatch.
/// </summary>
internal sealed class TelegramHandlerCandidateSet
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<TelegramHandlerCandidate>> _statefulByState;

    private TelegramHandlerCandidateSet(
        IReadOnlyList<TelegramHandlerCandidate> stateless,
        IReadOnlyDictionary<string, IReadOnlyList<TelegramHandlerCandidate>> statefulByState)
    {
        Stateless = stateless;
        _statefulByState = statefulByState;
    }

    public IReadOnlyList<TelegramHandlerCandidate> Stateless { get; }

    public bool HasStatefulCandidates => _statefulByState.Count > 0;

    public IReadOnlyList<TelegramHandlerCandidate> GetStatefulCandidates(string currentState)
    {
        return _statefulByState.TryGetValue(currentState, out var candidates)
            ? candidates
            : TelegramHandlerCandidateStateIndex.EmptyCandidates;
    }

    public static TelegramHandlerCandidateSet Create(IReadOnlyList<TelegramHandlerDescriptor> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var stateless = new List<TelegramHandlerCandidate>();
        var stateful = new Dictionary<string, List<TelegramHandlerCandidate>>(StringComparer.Ordinal);

        for (var index = 0; index < handlers.Count; index++)
        {
            var handler = handlers[index];
            var candidate = TelegramHandlerCandidate.Create(handler);

            if (handler.States.Count == 0)
            {
                stateless.Add(candidate);
                continue;
            }

            TelegramHandlerCandidateStateIndex.Add(stateful, handler.States, candidate);
        }

        return new TelegramHandlerCandidateSet(
            stateless.ToArray(),
            TelegramHandlerCandidateStateIndex.Freeze(stateful));
    }
}
