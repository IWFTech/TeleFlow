namespace TeleFlow.Telegram.Internal.Handlers;

/// <summary>
/// Pre-indexes Telegram error handlers so exception dispatch does not rebuild candidate groups per failure.
/// </summary>
internal sealed class TelegramErrorHandlerIndex
{
    private readonly Dictionary<string, TelegramErrorHandlerGroup> _moduleGroups;
    private readonly TelegramErrorHandlerGroup _globalGroup;

    public TelegramErrorHandlerIndex(IEnumerable<TelegramErrorHandlerDescriptor> errorHandlers)
    {
        ArgumentNullException.ThrowIfNull(errorHandlers);

        var moduleGroups = new Dictionary<string, List<TelegramErrorHandlerDescriptor>>(StringComparer.Ordinal);
        var globalHandlers = new List<TelegramErrorHandlerDescriptor>();

        foreach (var handler in errorHandlers)
        {
            if (handler.ModuleName is null)
            {
                globalHandlers.Add(handler);
                continue;
            }

            if (!moduleGroups.TryGetValue(handler.ModuleName, out var handlers))
            {
                handlers = [];
                moduleGroups.Add(handler.ModuleName, handlers);
            }

            handlers.Add(handler);
        }

        _moduleGroups = new Dictionary<string, TelegramErrorHandlerGroup>(moduleGroups.Count, StringComparer.Ordinal);

        foreach (var (moduleName, handlers) in moduleGroups)
        {
            _moduleGroups.Add(moduleName, new TelegramErrorHandlerGroup(handlers));
        }

        _globalGroup = new TelegramErrorHandlerGroup(globalHandlers);
        HasHandlers = _moduleGroups.Count > 0 || _globalGroup.HasHandlers;
    }

    /// <summary>
    /// Gets whether the index contains at least one error handler.
    /// </summary>
    public bool HasHandlers { get; }

    /// <summary>
    /// Enumerates compatible error handlers in dispatcher invocation order.
    /// </summary>
    public IEnumerable<TelegramErrorHandlerDescriptor> GetCandidates(
        TelegramRouteSelection selection,
        TelegramUpdateContext telegramContext,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(telegramContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (selection.Handler.ModuleName is { } moduleName &&
            _moduleGroups.TryGetValue(moduleName, out var moduleGroup))
        {
            foreach (var candidate in moduleGroup.GetCandidates(selection.RouteValues, telegramContext, exception))
            {
                yield return candidate;
            }
        }

        foreach (var candidate in _globalGroup.GetCandidates(selection.RouteValues, telegramContext, exception))
        {
            yield return candidate;
        }
    }

    private sealed class TelegramErrorHandlerGroup
    {
        private readonly Dictionary<Type, TelegramErrorHandlerDescriptor[]> _typedHandlers;
        private readonly TelegramErrorHandlerDescriptor[] _catchAllHandlers;

        public TelegramErrorHandlerGroup(IEnumerable<TelegramErrorHandlerDescriptor> handlers)
        {
            ArgumentNullException.ThrowIfNull(handlers);

            var typedHandlers = new Dictionary<Type, List<TelegramErrorHandlerDescriptor>>();
            var catchAllHandlers = new List<TelegramErrorHandlerDescriptor>();

            foreach (var handler in handlers.OrderBy(static handler => handler.RegistrationOrder))
            {
                if (handler.ExceptionType is null)
                {
                    catchAllHandlers.Add(handler);
                    continue;
                }

                if (!typedHandlers.TryGetValue(handler.ExceptionType, out var typedGroup))
                {
                    typedGroup = [];
                    typedHandlers.Add(handler.ExceptionType, typedGroup);
                }

                typedGroup.Add(handler);
            }

            _typedHandlers = new Dictionary<Type, TelegramErrorHandlerDescriptor[]>(typedHandlers.Count);

            foreach (var (exceptionType, typedGroup) in typedHandlers)
            {
                _typedHandlers.Add(exceptionType, typedGroup.ToArray());
            }

            _catchAllHandlers = catchAllHandlers.ToArray();
            HasHandlers = _typedHandlers.Count > 0 || _catchAllHandlers.Length > 0;
        }

        public bool HasHandlers { get; }

        public IEnumerable<TelegramErrorHandlerDescriptor> GetCandidates(
            IReadOnlyDictionary<string, object?> routeValues,
            TelegramUpdateContext telegramContext,
            Exception exception)
        {
            var exceptionType = exception.GetType();

            while (exceptionType is not null)
            {
                if (_typedHandlers.TryGetValue(exceptionType, out var typedHandlers))
                {
                    foreach (var handler in typedHandlers)
                    {
                        if (IsCompatibleErrorHandler(handler, routeValues, telegramContext))
                        {
                            yield return handler;
                        }
                    }
                }

                exceptionType = exceptionType.BaseType;
            }

            foreach (var handler in _catchAllHandlers)
            {
                if (IsCompatibleErrorHandler(handler, routeValues, telegramContext))
                {
                    yield return handler;
                }
            }
        }
    }

    private static bool IsCompatibleErrorHandler(
        TelegramErrorHandlerDescriptor handler,
        IReadOnlyDictionary<string, object?> routeValues,
        TelegramUpdateContext telegramContext)
    {
        if (handler.TelegramContextType is not null &&
            !handler.TelegramContextType.IsInstanceOfType(telegramContext))
        {
            return false;
        }

        if (!HasCompatibleRouteValues(handler, routeValues))
        {
            return false;
        }

        return true;
    }

    private static bool HasCompatibleRouteValues(
        TelegramErrorHandlerDescriptor handler,
        IReadOnlyDictionary<string, object?> routeValues)
    {
        foreach (var parameter in handler.Parameters)
        {
            if (parameter.Kind != TelegramErrorHandlerParameterKind.RouteValue)
            {
                continue;
            }

            if (!HasCompatibleRouteValue(parameter, routeValues))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasCompatibleRouteValue(
        TelegramErrorHandlerParameterDescriptor parameter,
        IReadOnlyDictionary<string, object?> routeValues)
    {
        return TryGetRouteValue(parameter, routeValues, out var value) &&
               IsRouteValueAssignable(parameter.ParameterType, value);
    }

    private static bool TryGetRouteValue(
        TelegramErrorHandlerParameterDescriptor parameter,
        IReadOnlyDictionary<string, object?> routeValues,
        out object? value)
    {
        value = null;

        return !string.IsNullOrWhiteSpace(parameter.Name) &&
               routeValues.TryGetValue(parameter.Name, out value);
    }

    private static bool IsRouteValueAssignable(Type parameterType, object? value)
    {
        if (value is null)
        {
            return IsNullableRouteValue(parameterType);
        }

        var targetType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;

        return targetType.IsInstanceOfType(value);
    }

    private static bool IsNullableRouteValue(Type parameterType)
    {
        return !parameterType.IsValueType ||
               Nullable.GetUnderlyingType(parameterType) is not null;
    }
}
