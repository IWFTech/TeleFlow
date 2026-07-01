using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure handler metadata emitted by TeleFlow source generators.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TelegramGeneratedHandlerDescriptor
{
    public TelegramGeneratedHandlerDescriptor(
        Type handlerType,
        string methodName,
        TelegramGeneratedHandlerKind kind,
        int registrationOrder,
        string? moduleName,
        string? command,
        Type? callbackPayloadType,
        IReadOnlyList<TelegramGeneratedTextFilterDescriptor> textFilters,
        IReadOnlyList<string> states,
        IReadOnlyList<TelegramGeneratedHandlerParameterDescriptor> parameters,
        TelegramGeneratedHandlerInvoker invoker)
        : this(
            handlerType,
            methodName,
            kind,
            GetLegacyRouteKind(kind),
            routePattern: command,
            commandPrefixes: ["/"],
            allowSpaceAfterPrefix: false,
            ignoreCase: true,
            registrationOrder,
            moduleName,
            command,
            callbackPayloadType,
            textFilters,
            filters: [],
            chatMemberTransitions: [],
            roleRequirements: [],
            states,
            parameters,
            invoker)
    {
    }

    public TelegramGeneratedHandlerDescriptor(
        Type handlerType,
        string methodName,
        TelegramGeneratedHandlerKind kind,
        TelegramGeneratedRouteKind routeKind,
        string? routePattern,
        IReadOnlyList<string>? commandPrefixes,
        bool allowSpaceAfterPrefix,
        bool ignoreCase,
        int registrationOrder,
        string? moduleName,
        string? command,
        Type? callbackPayloadType,
        IReadOnlyList<TelegramGeneratedTextFilterDescriptor> textFilters,
        IReadOnlyList<string> states,
        IReadOnlyList<TelegramGeneratedHandlerParameterDescriptor> parameters,
        TelegramGeneratedHandlerInvoker invoker)
        : this(
            handlerType,
            methodName,
            kind,
            routeKind,
            routePattern,
            commandPrefixes,
            allowSpaceAfterPrefix,
            ignoreCase,
            registrationOrder,
            moduleName,
            command,
            callbackPayloadType,
            textFilters,
            filters: [],
            chatMemberTransitions: [],
            roleRequirements: [],
            states,
            parameters,
            invoker)
    {
    }

    public TelegramGeneratedHandlerDescriptor(
        Type handlerType,
        string methodName,
        TelegramGeneratedHandlerKind kind,
        TelegramGeneratedRouteKind routeKind,
        string? routePattern,
        IReadOnlyList<string>? commandPrefixes,
        bool allowSpaceAfterPrefix,
        bool ignoreCase,
        int registrationOrder,
        string? moduleName,
        string? command,
        Type? callbackPayloadType,
        IReadOnlyList<TelegramGeneratedTextFilterDescriptor> textFilters,
        IReadOnlyList<TelegramGeneratedFilterDescriptor> filters,
        IReadOnlyList<string> states,
        IReadOnlyList<TelegramGeneratedHandlerParameterDescriptor> parameters,
        TelegramGeneratedHandlerInvoker invoker)
        : this(
            handlerType,
            methodName,
            kind,
            routeKind,
            routePattern,
            commandPrefixes,
            allowSpaceAfterPrefix,
            ignoreCase,
            registrationOrder,
            moduleName,
            command,
            callbackPayloadType,
            textFilters,
            filters,
            chatMemberTransitions: [],
            roleRequirements: [],
            states,
            parameters,
            invoker)
    {
    }

    public TelegramGeneratedHandlerDescriptor(
        Type handlerType,
        string methodName,
        TelegramGeneratedHandlerKind kind,
        TelegramGeneratedRouteKind routeKind,
        string? routePattern,
        IReadOnlyList<string>? commandPrefixes,
        bool allowSpaceAfterPrefix,
        bool ignoreCase,
        int registrationOrder,
        string? moduleName,
        string? command,
        Type? callbackPayloadType,
        IReadOnlyList<TelegramGeneratedTextFilterDescriptor> textFilters,
        IReadOnlyList<TelegramGeneratedFilterDescriptor> filters,
        IReadOnlyList<TelegramGeneratedChatMemberTransitionDescriptor> chatMemberTransitions,
        IReadOnlyList<string> states,
        IReadOnlyList<TelegramGeneratedHandlerParameterDescriptor> parameters,
        TelegramGeneratedHandlerInvoker invoker)
        : this(
            handlerType,
            methodName,
            kind,
            routeKind,
            routePattern,
            commandPrefixes,
            allowSpaceAfterPrefix,
            ignoreCase,
            registrationOrder,
            moduleName,
            command,
            callbackPayloadType,
            textFilters,
            filters,
            chatMemberTransitions,
            roleRequirements: [],
            states,
            parameters,
            invoker)
    {
    }

    public TelegramGeneratedHandlerDescriptor(
        Type handlerType,
        string methodName,
        TelegramGeneratedHandlerKind kind,
        TelegramGeneratedRouteKind routeKind,
        string? routePattern,
        IReadOnlyList<string>? commandPrefixes,
        bool allowSpaceAfterPrefix,
        bool ignoreCase,
        int registrationOrder,
        string? moduleName,
        string? command,
        Type? callbackPayloadType,
        IReadOnlyList<TelegramGeneratedTextFilterDescriptor> textFilters,
        IReadOnlyList<TelegramGeneratedFilterDescriptor> filters,
        IReadOnlyList<TelegramGeneratedChatMemberTransitionDescriptor> chatMemberTransitions,
        IReadOnlyList<TelegramGeneratedRoleRequirementDescriptor> roleRequirements,
        IReadOnlyList<string> states,
        IReadOnlyList<TelegramGeneratedHandlerParameterDescriptor> parameters,
        TelegramGeneratedHandlerInvoker invoker,
        string? sceneName = null,
        IReadOnlyList<TelegramGeneratedRouteValueDescriptor>? routeValues = null,
        TelegramGeneratedAutoAnswerCallbackDescriptor? autoAnswerCallback = null)
    {
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(textFilters);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(chatMemberTransitions);
        ArgumentNullException.ThrowIfNull(roleRequirements);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(invoker);

        HandlerType = handlerType;
        MethodName = methodName;
        Kind = kind;
        RouteKind = routeKind;
        RoutePattern = routePattern;
        CommandPrefixes = CopyValues(commandPrefixes ?? ["/"]);
        AllowSpaceAfterPrefix = allowSpaceAfterPrefix;
        IgnoreCase = ignoreCase;
        RegistrationOrder = registrationOrder;
        ModuleName = moduleName;
        Command = command;
        CallbackPayloadType = callbackPayloadType;
        TextFilters = CopyValues(textFilters);
        Filters = CopyValues(filters);
        ChatMemberTransitions = CopyValues(chatMemberTransitions);
        RoleRequirements = CopyValues(roleRequirements);
        States = CopyValues(states);
        Parameters = CopyValues(parameters);
        Invoker = invoker;
        SceneName = sceneName;
        RouteValues = CopyValues(routeValues);
        AutoAnswerCallback = autoAnswerCallback;
    }

    private static TelegramGeneratedRouteKind GetLegacyRouteKind(TelegramGeneratedHandlerKind kind)
    {
        return kind switch
        {
            TelegramGeneratedHandlerKind.Command => TelegramGeneratedRouteKind.CommandExact,
            TelegramGeneratedHandlerKind.Message => TelegramGeneratedRouteKind.MessageAny,
            TelegramGeneratedHandlerKind.Callback => TelegramGeneratedRouteKind.Callback,
            TelegramGeneratedHandlerKind.ChatMember => TelegramGeneratedRouteKind.ChatMemberUpdated,
            _ => throw new InvalidOperationException($"Unsupported generated Telegram handler kind '{kind}'.")
        };
    }

    public Type HandlerType { get; }

    public string MethodName { get; }

    public TelegramGeneratedHandlerKind Kind { get; }

    public TelegramGeneratedRouteKind RouteKind { get; }

    public string? RoutePattern { get; }

    public IReadOnlyList<string> CommandPrefixes { get; }

    public bool AllowSpaceAfterPrefix { get; }

    public bool IgnoreCase { get; }

    public int RegistrationOrder { get; }

    public string? ModuleName { get; }

    public string? SceneName { get; }

    public string? Command { get; }

    public Type? CallbackPayloadType { get; }

    public IReadOnlyList<TelegramGeneratedTextFilterDescriptor> TextFilters { get; }

    public IReadOnlyList<TelegramGeneratedFilterDescriptor> Filters { get; }

    public IReadOnlyList<TelegramGeneratedChatMemberTransitionDescriptor> ChatMemberTransitions { get; }

    public IReadOnlyList<TelegramGeneratedRoleRequirementDescriptor> RoleRequirements { get; }

    public IReadOnlyList<string> States { get; }

    public IReadOnlyList<TelegramGeneratedHandlerParameterDescriptor> Parameters { get; }

    public TelegramGeneratedHandlerInvoker Invoker { get; }

    public IReadOnlyList<TelegramGeneratedRouteValueDescriptor> RouteValues { get; }

    public TelegramGeneratedAutoAnswerCallbackDescriptor? AutoAnswerCallback { get; }

    private static TValue[] CopyValues<TValue>(IReadOnlyList<TValue>? values)
    {
        return values is null || values.Count == 0
            ? []
            : values.ToArray();
    }
}
