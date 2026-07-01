namespace TeleFlow.Telegram.Internal.Handlers;

/// <summary>
/// Groups callback handler candidates by state and payload shape so callback dispatch can preserve
/// typed-before-raw routing without scanning unrelated callback handlers on every callback query.
/// </summary>
internal sealed class TelegramCallbackHandlerCandidateSet
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<TelegramHandlerCandidate>> _statefulTypedByState;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<TelegramHandlerCandidate>> _statefulRawByState;

    private TelegramCallbackHandlerCandidateSet(
        IReadOnlyList<TelegramHandlerCandidate> statelessTyped,
        IReadOnlyList<TelegramHandlerCandidate> statelessRaw,
        IReadOnlyDictionary<string, IReadOnlyList<TelegramHandlerCandidate>> statefulTypedByState,
        IReadOnlyDictionary<string, IReadOnlyList<TelegramHandlerCandidate>> statefulRawByState)
    {
        StatelessTyped = statelessTyped;
        StatelessRaw = statelessRaw;
        _statefulTypedByState = statefulTypedByState;
        _statefulRawByState = statefulRawByState;
    }

    public IReadOnlyList<TelegramHandlerCandidate> StatelessTyped { get; }

    public IReadOnlyList<TelegramHandlerCandidate> StatelessRaw { get; }

    public bool HasStatefulCandidates => _statefulTypedByState.Count > 0 || _statefulRawByState.Count > 0;

    public IReadOnlyList<TelegramHandlerCandidate> GetStatefulTypedCandidates(string currentState)
    {
        return _statefulTypedByState.TryGetValue(currentState, out var candidates)
            ? candidates
            : TelegramHandlerCandidateStateIndex.EmptyCandidates;
    }

    public IReadOnlyList<TelegramHandlerCandidate> GetStatefulRawCandidates(string currentState)
    {
        return _statefulRawByState.TryGetValue(currentState, out var candidates)
            ? candidates
            : TelegramHandlerCandidateStateIndex.EmptyCandidates;
    }

    public static TelegramCallbackHandlerCandidateSet Create(IReadOnlyList<TelegramHandlerDescriptor> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var statelessTyped = new List<TelegramHandlerCandidate>();
        var statelessRaw = new List<TelegramHandlerCandidate>();
        var statefulTyped = new Dictionary<string, List<TelegramHandlerCandidate>>(StringComparer.Ordinal);
        var statefulRaw = new Dictionary<string, List<TelegramHandlerCandidate>>(StringComparer.Ordinal);

        for (var index = 0; index < handlers.Count; index++)
        {
            var handler = handlers[index];
            var candidate = TelegramHandlerCandidate.Create(handler);
            var isTyped = handler.CallbackPayloadType is not null;

            if (handler.States.Count == 0)
            {
                if (isTyped)
                {
                    statelessTyped.Add(candidate);
                }
                else
                {
                    statelessRaw.Add(candidate);
                }

                continue;
            }

            TelegramHandlerCandidateStateIndex.Add(
                isTyped ? statefulTyped : statefulRaw,
                handler.States,
                candidate);
        }

        return new TelegramCallbackHandlerCandidateSet(
            statelessTyped.ToArray(),
            statelessRaw.ToArray(),
            TelegramHandlerCandidateStateIndex.Freeze(statefulTyped),
            TelegramHandlerCandidateStateIndex.Freeze(statefulRaw));
    }
}
