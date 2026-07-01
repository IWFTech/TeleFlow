using System.Reflection;

namespace TeleFlow.Telegram.Internal.Handlers;

internal delegate ValueTask TelegramHandlerGeneratedInvoker(
    IServiceProvider services,
    object?[] arguments,
    CancellationToken cancellationToken);

internal sealed class TelegramHandlerDescriptor
{
    public TelegramHandlerDescriptor(
        Type handlerType,
        MethodInfo method,
        TelegramRouteDescriptor route,
        int registrationOrder,
        string? moduleName,
        IReadOnlyList<string> states,
        IReadOnlyList<TelegramHandlerParameterDescriptor> parameters,
        string? sceneName = null,
        TelegramAutoAnswerCallbackDescriptor? autoAnswerCallback = null)
        : this(
            handlerType,
            method,
            method.Name,
            generatedInvoker: null,
            route,
            registrationOrder,
            moduleName,
            sceneName,
            autoAnswerCallback,
            states,
            parameters)
    {
    }

    public TelegramHandlerDescriptor(
        Type handlerType,
        MethodInfo method,
        TelegramHandlerKind kind,
        int registrationOrder,
        string? moduleName,
        string? command,
        IReadOnlyList<TelegramTextFilter> textFilters,
        IReadOnlyList<string> states,
        Type? callbackPayloadType,
        IReadOnlyList<TelegramHandlerParameterDescriptor> parameters,
        string? sceneName = null,
        TelegramAutoAnswerCallbackDescriptor? autoAnswerCallback = null)
        : this(
            handlerType,
            method,
            method.Name,
            generatedInvoker: null,
            new TelegramRouteDescriptor(kind, command, textFilters, callbackPayloadType),
            registrationOrder,
            moduleName,
            sceneName,
            autoAnswerCallback,
            states,
            parameters)
    {
    }

    public TelegramHandlerDescriptor(
        Type handlerType,
        string methodName,
        TelegramHandlerGeneratedInvoker generatedInvoker,
        TelegramRouteDescriptor route,
        int registrationOrder,
        string? moduleName,
        IReadOnlyList<string> states,
        IReadOnlyList<TelegramHandlerParameterDescriptor> parameters,
        string? sceneName = null,
        TelegramAutoAnswerCallbackDescriptor? autoAnswerCallback = null)
        : this(
            handlerType,
            method: null,
            methodName,
            generatedInvoker,
            route,
            registrationOrder,
            moduleName,
            sceneName,
            autoAnswerCallback,
            states,
            parameters)
    {
    }

    public TelegramHandlerDescriptor(
        Type handlerType,
        string methodName,
        TelegramHandlerGeneratedInvoker generatedInvoker,
        TelegramHandlerKind kind,
        int registrationOrder,
        string? moduleName,
        string? command,
        IReadOnlyList<TelegramTextFilter> textFilters,
        IReadOnlyList<string> states,
        Type? callbackPayloadType,
        IReadOnlyList<TelegramHandlerParameterDescriptor> parameters,
        string? sceneName = null,
        TelegramAutoAnswerCallbackDescriptor? autoAnswerCallback = null)
        : this(
            handlerType,
            method: null,
            methodName,
            generatedInvoker,
            new TelegramRouteDescriptor(kind, command, textFilters, callbackPayloadType),
            registrationOrder,
            moduleName,
            sceneName,
            autoAnswerCallback,
            states,
            parameters)
    {
    }

    private TelegramHandlerDescriptor(
        Type handlerType,
        MethodInfo? method,
        string methodName,
        TelegramHandlerGeneratedInvoker? generatedInvoker,
        TelegramRouteDescriptor route,
        int registrationOrder,
        string? moduleName,
        string? sceneName,
        TelegramAutoAnswerCallbackDescriptor? autoAnswerCallback,
        IReadOnlyList<string> states,
        IReadOnlyList<TelegramHandlerParameterDescriptor> parameters)
    {
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(parameters);

        HandlerType = handlerType;
        Method = method;
        MethodName = methodName;
        GeneratedInvoker = generatedInvoker;
        RegistrationOrder = registrationOrder;
        ModuleName = moduleName;
        SceneName = sceneName;
        AutoAnswerCallback = autoAnswerCallback;
        Route = route;
        States = states;
        Parameters = parameters;
    }

    public Type HandlerType { get; }

    public MethodInfo? Method { get; }

    public string MethodName { get; }

    public TelegramHandlerGeneratedInvoker? GeneratedInvoker { get; }

    public TelegramHandlerKind Kind => Route.Kind;

    public int RegistrationOrder { get; }

    public string? ModuleName { get; }

    public string? SceneName { get; }

    public TelegramAutoAnswerCallbackDescriptor? AutoAnswerCallback { get; }

    public TelegramRouteDescriptor Route { get; }

    public string? Command => Route.Command;

    public IReadOnlyList<TelegramTextFilter> TextFilters => Route.TextFilters;

    public IReadOnlyList<string> States { get; }

    public Type? CallbackPayloadType => Route.CallbackPayloadType;

    public IReadOnlyList<TelegramHandlerParameterDescriptor> Parameters { get; }
}
