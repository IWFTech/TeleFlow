namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed class TelegramRouteDescriptor
{
    public TelegramRouteDescriptor(
        TelegramHandlerKind kind,
        string? command,
        IReadOnlyList<TelegramTextFilter> textFilters,
        Type? callbackPayloadType)
        : this(
            MapLegacyRouteKind(kind, command),
            command,
            TelegramCommandPolicy.Default,
            textFilters,
            callbackPayloadType,
            routeValues: [],
            filters: [],
            chatMemberTransitions: [],
            roleRequirements: [])
    {
    }

    public TelegramRouteDescriptor(
        TelegramRouteKind routeKind,
        string? pattern,
        TelegramCommandPolicy? commandPolicy,
        IReadOnlyList<TelegramTextFilter> textFilters,
        Type? callbackPayloadType,
        IReadOnlyList<TelegramRouteValueDescriptor> routeValues)
        : this(
            routeKind,
            pattern,
            commandPolicy,
            textFilters,
            callbackPayloadType,
            routeValues,
            filters: [],
            chatMemberTransitions: [],
            roleRequirements: [])
    {
    }

    public TelegramRouteDescriptor(
        TelegramRouteKind routeKind,
        string? pattern,
        TelegramCommandPolicy? commandPolicy,
        IReadOnlyList<TelegramTextFilter> textFilters,
        Type? callbackPayloadType,
        IReadOnlyList<TelegramRouteValueDescriptor> routeValues,
        IReadOnlyList<TelegramFilterDescriptor> filters)
        : this(
            routeKind,
            pattern,
            commandPolicy,
            textFilters,
            callbackPayloadType,
            routeValues,
            filters,
            chatMemberTransitions: [],
            roleRequirements: [])
    {
    }

    public TelegramRouteDescriptor(
        TelegramRouteKind routeKind,
        string? pattern,
        TelegramCommandPolicy? commandPolicy,
        IReadOnlyList<TelegramTextFilter> textFilters,
        Type? callbackPayloadType,
        IReadOnlyList<TelegramRouteValueDescriptor> routeValues,
        IReadOnlyList<TelegramFilterDescriptor> filters,
        IReadOnlyList<TelegramChatMemberTransitionDescriptor> chatMemberTransitions)
        : this(
            routeKind,
            pattern,
            commandPolicy,
            textFilters,
            callbackPayloadType,
            routeValues,
            filters,
            chatMemberTransitions,
            roleRequirements: [])
    {
    }

    public TelegramRouteDescriptor(
        TelegramRouteKind routeKind,
        string? pattern,
        TelegramCommandPolicy? commandPolicy,
        IReadOnlyList<TelegramTextFilter> textFilters,
        Type? callbackPayloadType,
        IReadOnlyList<TelegramRouteValueDescriptor> routeValues,
        IReadOnlyList<TelegramFilterDescriptor> filters,
        IReadOnlyList<TelegramChatMemberTransitionDescriptor> chatMemberTransitions,
        IReadOnlyList<TelegramRoleRequirementDescriptor> roleRequirements)
    {
        ArgumentNullException.ThrowIfNull(textFilters);
        ArgumentNullException.ThrowIfNull(routeValues);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(chatMemberTransitions);
        ArgumentNullException.ThrowIfNull(roleRequirements);

        var normalizedPattern = NormalizeCommandPattern(routeKind, pattern);

        RouteKind = routeKind;
        Kind = GetHandlerKind(routeKind);
        Pattern = normalizedPattern;
        CommandPolicy = commandPolicy ?? TelegramCommandPolicy.Default;
        TextFilters = textFilters;
        CallbackPayloadType = callbackPayloadType;
        RouteValues = routeValues;
        Filters = filters;
        ChatMemberTransitions = chatMemberTransitions;
        RoleRequirements = roleRequirements;
        Matcher = TelegramRouteMatcher.Create(routeKind, normalizedPattern, CommandPolicy.IgnoreCase);
        Specificity = GetSpecificity(routeKind, normalizedPattern);
    }

    private static TelegramRouteKind MapLegacyRouteKind(
        TelegramHandlerKind kind,
        string? command)
    {
        return kind switch
        {
            TelegramHandlerKind.Command => TelegramRouteKind.CommandExact,
            TelegramHandlerKind.Callback => TelegramRouteKind.Callback,
            TelegramHandlerKind.Message when command is null => TelegramRouteKind.MessageAny,
            _ => throw new InvalidOperationException($"Unsupported Telegram handler kind '{kind}'.")
        };
    }

    private static TelegramHandlerKind GetHandlerKind(TelegramRouteKind routeKind)
    {
        return routeKind switch
        {
            TelegramRouteKind.CommandExact or
                TelegramRouteKind.CommandTemplate or
                TelegramRouteKind.CommandRegex => TelegramHandlerKind.Command,
            TelegramRouteKind.Callback => TelegramHandlerKind.Callback,
            TelegramRouteKind.ChatMemberUpdated or
                TelegramRouteKind.MyChatMemberUpdated => TelegramHandlerKind.ChatMember,
            _ => TelegramHandlerKind.Message
        };
    }

    public TelegramHandlerKind Kind { get; }

    public TelegramRouteKind RouteKind { get; }

    public string? Pattern { get; }

    public string? Command => Kind == TelegramHandlerKind.Command ? Pattern : null;

    public TelegramCommandPolicy CommandPolicy { get; }

    public IReadOnlyList<TelegramTextFilter> TextFilters { get; }

    public Type? CallbackPayloadType { get; }

    public IReadOnlyList<TelegramRouteValueDescriptor> RouteValues { get; }

    public IReadOnlyList<TelegramFilterDescriptor> Filters { get; }

    public IReadOnlyList<TelegramChatMemberTransitionDescriptor> ChatMemberTransitions { get; }

    public IReadOnlyList<TelegramRoleRequirementDescriptor> RoleRequirements { get; }

    public TelegramRouteMatcher Matcher { get; }

    public int Specificity { get; }

    private static string? NormalizeCommandPattern(
        TelegramRouteKind routeKind,
        string? pattern)
    {
        if (pattern is null ||
            routeKind is not (TelegramRouteKind.CommandExact or TelegramRouteKind.CommandTemplate))
        {
            return pattern;
        }

        return TelegramCommandTextNormalizer.Normalize(pattern);
    }

    private static int GetSpecificity(
        TelegramRouteKind routeKind,
        string? pattern)
    {
        return routeKind is TelegramRouteKind.TextTemplate or TelegramRouteKind.CommandTemplate &&
               !string.IsNullOrWhiteSpace(pattern)
            ? TelegramTemplateRouteParser.GetSpecificity(pattern)
            : 0;
    }
}
