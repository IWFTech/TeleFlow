namespace TeleFlow.Telegram;

public sealed class TelegramErrorContext
{
    internal TelegramErrorContext(
        Exception exception,
        TelegramUpdateContext updateContext,
        Type handlerType,
        string handlerMethodName,
        string? moduleName,
        string? sceneName,
        IReadOnlyDictionary<string, object?> routeValues)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(updateContext);
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerMethodName);
        ArgumentNullException.ThrowIfNull(routeValues);

        Exception = exception;
        UpdateContext = updateContext;
        HandlerType = handlerType;
        HandlerMethodName = handlerMethodName;
        ModuleName = moduleName;
        SceneName = sceneName;
        RouteValues = routeValues;
    }

    public Exception Exception { get; }

    public TelegramUpdateContext UpdateContext { get; }

    public Type HandlerType { get; }

    public string HandlerMethodName { get; }

    public string? ModuleName { get; }

    public string? SceneName { get; }

    public IReadOnlyDictionary<string, object?> RouteValues { get; }
}
