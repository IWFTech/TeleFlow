namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed class TelegramGeneratedHandlerRegistry : ITelegramGeneratedHandlerRegistry
{
    private readonly List<TelegramGeneratedHandlerDescriptor> _descriptors = [];
    private readonly List<TelegramGeneratedErrorHandlerDescriptor> _errorDescriptors = [];

    public void RegisterHandler(TelegramGeneratedHandlerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        _descriptors.Add(descriptor);
    }

    public void RegisterErrorHandler(TelegramGeneratedErrorHandlerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        _errorDescriptors.Add(descriptor);
    }

    public IReadOnlyList<TelegramHandlerDescriptor> BuildDescriptors(
        int firstRegistrationOrder,
        Func<TelegramGeneratedHandlerDescriptor, bool>? predicate = null)
    {
        return _descriptors
            .OrderBy(static descriptor => descriptor.RegistrationOrder)
            .Where(descriptor => predicate?.Invoke(descriptor) ?? true)
            .Select((descriptor, index) => BuildDescriptor(descriptor, firstRegistrationOrder + index))
            .ToArray();
    }

    public IReadOnlyList<TelegramErrorHandlerDescriptor> BuildErrorDescriptors(
        int firstRegistrationOrder,
        Func<TelegramGeneratedErrorHandlerDescriptor, bool>? predicate = null)
    {
        return _errorDescriptors
            .OrderBy(static descriptor => descriptor.RegistrationOrder)
            .Where(descriptor => predicate?.Invoke(descriptor) ?? true)
            .Select((descriptor, index) => BuildErrorDescriptor(descriptor, firstRegistrationOrder + index))
            .ToArray();
    }

    public IReadOnlyList<Type> HandlerTypes => _descriptors
        .Select(static descriptor => descriptor.HandlerType)
        .Concat(_errorDescriptors.Select(static descriptor => descriptor.HandlerType))
        .Distinct()
        .ToArray();

    private static TelegramHandlerDescriptor BuildDescriptor(
        TelegramGeneratedHandlerDescriptor descriptor,
        int registrationOrder)
    {
        var kind = MapKind(descriptor.Kind);
        var parameters = descriptor.Parameters
            .Select(static parameter => new TelegramHandlerParameterDescriptor(
                parameter.ParameterType,
                MapParameterKind(parameter.Kind),
                parameter.Name))
            .ToArray();
        var routeValues = descriptor.RouteValues.Count > 0
            ? descriptor.RouteValues
                .Select(static value => new TelegramRouteValueDescriptor(
                    value.Name,
                    value.ValueType,
                    value.IsOptional))
                .ToArray()
            : parameters
                .Where(static parameter => parameter.Kind == TelegramHandlerParameterKind.RouteValue)
                .Select(static parameter => new TelegramRouteValueDescriptor(
                    parameter.Name!,
                    Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType))
                .ToArray();
        var route = new TelegramRouteDescriptor(
            MapRouteKind(descriptor.RouteKind),
            descriptor.RoutePattern ?? descriptor.Command,
            new TelegramCommandPolicy(
                descriptor.CommandPrefixes,
                descriptor.AllowSpaceAfterPrefix,
                descriptor.IgnoreCase,
                descriptor.PrefixMode),
            descriptor.TextFilters
                .Select(static filter => new TelegramTextFilter(filter.Value, filter.Mode, filter.IgnoreCase))
                .ToArray(),
            descriptor.CallbackPayloadType,
            routeValues,
            descriptor.Filters
                .Select(filter => MapFilter(filter, kind))
                .ToArray(),
            descriptor.ChatMemberTransitions
                .Select(static transition => new TelegramChatMemberTransitionDescriptor(
                    transition.OldStatus,
                    transition.NewStatus))
                .ToArray(),
            descriptor.RoleRequirements
                .Select(static requirement => new TelegramRoleRequirementDescriptor(requirement.AllowedStatuses))
                .ToArray());

        return new TelegramHandlerDescriptor(
            descriptor.HandlerType,
            descriptor.MethodName,
            (services, arguments, cancellationToken) =>
                descriptor.Invoker(services, arguments, cancellationToken),
            route,
            registrationOrder,
            descriptor.ModuleName,
            descriptor.States.ToArray(),
            parameters,
            descriptor.SceneName,
            MapAutoAnswerCallback(descriptor.AutoAnswerCallback));
    }

    private static TelegramErrorHandlerDescriptor BuildErrorDescriptor(
        TelegramGeneratedErrorHandlerDescriptor descriptor,
        int registrationOrder)
    {
        var parameters = descriptor.Parameters
            .Select(static parameter => new TelegramErrorHandlerParameterDescriptor(
                parameter.ParameterType,
                MapErrorParameterKind(parameter.Kind),
                parameter.Name))
            .ToArray();

        return new TelegramErrorHandlerDescriptor(
            descriptor.HandlerType,
            descriptor.MethodName,
            (services, arguments, cancellationToken) =>
                descriptor.Invoker(services, arguments, cancellationToken),
            descriptor.ExceptionType,
            descriptor.TelegramContextType,
            registrationOrder,
            descriptor.ModuleName,
            parameters);
    }

    private static TelegramAutoAnswerCallbackDescriptor? MapAutoAnswerCallback(
        TelegramGeneratedAutoAnswerCallbackDescriptor? descriptor)
    {
        return descriptor is null
            ? null
            : new TelegramAutoAnswerCallbackDescriptor(
                descriptor.Enabled,
                descriptor.Text,
                descriptor.ShowAlert);
    }

    private static TelegramHandlerKind MapKind(TelegramGeneratedHandlerKind kind)
    {
        return kind switch
        {
            TelegramGeneratedHandlerKind.Command => TelegramHandlerKind.Command,
            TelegramGeneratedHandlerKind.Message => TelegramHandlerKind.Message,
            TelegramGeneratedHandlerKind.Callback => TelegramHandlerKind.Callback,
            TelegramGeneratedHandlerKind.ChatMember => TelegramHandlerKind.ChatMember,
            _ => throw new InvalidOperationException($"Unsupported generated Telegram handler kind '{kind}'.")
        };
    }

    private static TelegramRouteKind MapRouteKind(TelegramGeneratedRouteKind kind)
    {
        return kind switch
        {
            TelegramGeneratedRouteKind.MessageAny => TelegramRouteKind.MessageAny,
            TelegramGeneratedRouteKind.TextExact => TelegramRouteKind.TextExact,
            TelegramGeneratedRouteKind.CommandExact => TelegramRouteKind.CommandExact,
            TelegramGeneratedRouteKind.TextTemplate => TelegramRouteKind.TextTemplate,
            TelegramGeneratedRouteKind.CommandTemplate => TelegramRouteKind.CommandTemplate,
            TelegramGeneratedRouteKind.TextRegex => TelegramRouteKind.TextRegex,
            TelegramGeneratedRouteKind.CommandRegex => TelegramRouteKind.CommandRegex,
            TelegramGeneratedRouteKind.Callback => TelegramRouteKind.Callback,
            TelegramGeneratedRouteKind.ChatMemberUpdated => TelegramRouteKind.ChatMemberUpdated,
            TelegramGeneratedRouteKind.MyChatMemberUpdated => TelegramRouteKind.MyChatMemberUpdated,
            _ => throw new InvalidOperationException($"Unsupported generated Telegram route kind '{kind}'.")
        };
    }

    private static TelegramFilterDescriptor MapFilter(
        TelegramGeneratedFilterDescriptor descriptor,
        TelegramHandlerKind kind)
    {
        if (descriptor.Kind == TelegramGeneratedFilterKind.Custom)
        {
            if (descriptor.CustomFilterType is null)
            {
                throw new InvalidOperationException("Generated custom Telegram filter descriptor must provide a filter type.");
            }

            ValidateCustomFilterType(
                descriptor.CustomFilterType,
                descriptor.CustomFilterContextType,
                descriptor.CustomFilterAttribute,
                kind);

            if (descriptor.CustomFilterAttribute is null)
            {
                return descriptor.CustomFilterContextType is null
                    ? new TelegramFilterDescriptor(descriptor.CustomFilterType)
                    : new TelegramFilterDescriptor(
                        descriptor.CustomFilterType,
                        descriptor.CustomFilterContextType);
            }

            return descriptor.CustomFilterContextType is null
                ? new TelegramFilterDescriptor(
                    descriptor.CustomFilterType,
                    descriptor.CustomFilterAttribute)
                : new TelegramFilterDescriptor(
                    descriptor.CustomFilterType,
                    descriptor.CustomFilterContextType,
                    descriptor.CustomFilterAttribute);
        }

        var filterKind = MapFilterKind(descriptor.Kind);

        if (!TelegramFilterCompatibility.Supports(filterKind, kind))
        {
            throw new InvalidOperationException(
                $"Generated {TelegramFilterCompatibility.GetInvalidPlacementMessage(filterKind, kind)}");
        }

        return new TelegramFilterDescriptor(
            filterKind,
            descriptor.StringValues.ToArray(),
            descriptor.LongValues.ToArray());
    }

    private static void ValidateCustomFilterType(
        Type filterType,
        Type? contextType,
        Attribute? attribute,
        TelegramHandlerKind kind)
    {
        if (filterType.IsInterface ||
            filterType.IsAbstract ||
            filterType.ContainsGenericParameters)
        {
            throw new InvalidOperationException(
                $"Generated custom Telegram filter type '{filterType.FullName}' must be a concrete closed type.");
        }

        var expectedContextType = kind switch
        {
            TelegramHandlerKind.Callback => typeof(CallbackQueryContext),
            TelegramHandlerKind.ChatMember => typeof(ChatMemberUpdatedContext),
            _ => typeof(MessageContext)
        };

        if (contextType is not null)
        {
            if (contextType != expectedContextType &&
                contextType != typeof(TelegramUpdateContext))
            {
                throw new InvalidOperationException(
                    $"Generated custom Telegram filter type '{filterType.FullName}' is not compatible with {expectedContextType.Name} handlers.");
            }

            var requiredContract = attribute is null
                ? typeof(ITelegramFilter<>).MakeGenericType(contextType)
                : typeof(ITelegramFilter<,>).MakeGenericType(contextType, attribute.GetType());

            if (!requiredContract.IsAssignableFrom(filterType))
            {
                throw new InvalidOperationException(
                    attribute is null
                        ? $"Generated custom Telegram filter type '{filterType.FullName}' must implement ITelegramFilter<{contextType.Name}>."
                        : $"Generated parameterized custom Telegram filter type '{filterType.FullName}' must implement ITelegramFilter<{contextType.Name}, {attribute.GetType().Name}>.");
            }

            return;
        }

        if (attribute is null)
        {
            var contextTypes = GetCustomFilterContextTypes(filterType, attributeType: null);

            if (contextTypes.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Generated custom Telegram filter type '{filterType.FullName}' must implement ITelegramFilter<TContext>.");
            }

            if (!SupportsContextType(contextTypes, expectedContextType))
            {
                throw new InvalidOperationException(
                    $"Generated custom Telegram filter type '{filterType.FullName}' is not compatible with {expectedContextType.Name} handlers.");
            }

            return;
        }

        var attributeType = attribute.GetType();

        if (attributeType.IsGenericType)
        {
            throw new InvalidOperationException(
                $"Generated custom Telegram filter attribute type '{attributeType.FullName}' must be non-generic.");
        }

        var typedContextTypes = GetCustomFilterContextTypes(filterType, attributeType);

        if (typedContextTypes.Length == 0)
        {
            throw new InvalidOperationException(
                $"Generated parameterized custom Telegram filter type '{filterType.FullName}' must implement ITelegramFilter<TContext, {attributeType.Name}>.");
        }

        if (!SupportsContextType(typedContextTypes, expectedContextType))
        {
            throw new InvalidOperationException(
                $"Generated custom Telegram filter type '{filterType.FullName}' is not compatible with {expectedContextType.Name} handlers.");
        }
    }

    private static Type[] GetCustomFilterContextTypes(
        Type filterType,
        Type? attributeType)
    {
        var interfaces = filterType.GetInterfaces();

        if (attributeType is null)
        {
            return interfaces
                .Where(static type => type.IsGenericType &&
                                      type.GetGenericTypeDefinition() == typeof(ITelegramFilter<>))
                .Select(static type => type.GetGenericArguments()[0])
                .ToArray();
        }

        return interfaces
            .Where(type => type.IsGenericType &&
                           type.GetGenericTypeDefinition() == typeof(ITelegramFilter<,>) &&
                           type.GetGenericArguments()[1] == attributeType)
            .Select(static type => type.GetGenericArguments()[0])
            .ToArray();
    }

    private static bool SupportsContextType(
        IReadOnlyCollection<Type> contextTypes,
        Type expectedContextType)
    {
        return contextTypes.Contains(expectedContextType) ||
               contextTypes.Contains(typeof(TelegramUpdateContext));
    }

    private static TelegramFilterKind MapFilterKind(TelegramGeneratedFilterKind kind)
    {
        return kind switch
        {
            TelegramGeneratedFilterKind.ChatType => TelegramFilterKind.ChatType,
            TelegramGeneratedFilterKind.ChatId => TelegramFilterKind.ChatId,
            TelegramGeneratedFilterKind.ChatUsername => TelegramFilterKind.ChatUsername,
            TelegramGeneratedFilterKind.FromUser => TelegramFilterKind.FromUser,
            TelegramGeneratedFilterKind.HasText => TelegramFilterKind.HasText,
            TelegramGeneratedFilterKind.HasPhoto => TelegramFilterKind.HasPhoto,
            TelegramGeneratedFilterKind.HasDocument => TelegramFilterKind.HasDocument,
            TelegramGeneratedFilterKind.HasCaption => TelegramFilterKind.HasCaption,
            TelegramGeneratedFilterKind.HasVideo => TelegramFilterKind.HasVideo,
            TelegramGeneratedFilterKind.HasAnimation => TelegramFilterKind.HasAnimation,
            TelegramGeneratedFilterKind.HasAudio => TelegramFilterKind.HasAudio,
            TelegramGeneratedFilterKind.HasVoice => TelegramFilterKind.HasVoice,
            TelegramGeneratedFilterKind.HasVideoNote => TelegramFilterKind.HasVideoNote,
            TelegramGeneratedFilterKind.HasSticker => TelegramFilterKind.HasSticker,
            TelegramGeneratedFilterKind.HasContact => TelegramFilterKind.HasContact,
            TelegramGeneratedFilterKind.HasLocation => TelegramFilterKind.HasLocation,
            TelegramGeneratedFilterKind.HasVenue => TelegramFilterKind.HasVenue,
            TelegramGeneratedFilterKind.HasPoll => TelegramFilterKind.HasPoll,
            TelegramGeneratedFilterKind.HasDice => TelegramFilterKind.HasDice,
            TelegramGeneratedFilterKind.FromBot => TelegramFilterKind.FromBot,
            TelegramGeneratedFilterKind.FromPremiumUser => TelegramFilterKind.FromPremiumUser,
            TelegramGeneratedFilterKind.IsReply => TelegramFilterKind.IsReply,
            TelegramGeneratedFilterKind.ReplyToBot => TelegramFilterKind.ReplyToBot,
            TelegramGeneratedFilterKind.MessageThreadId => TelegramFilterKind.MessageThreadId,
            TelegramGeneratedFilterKind.HasMessageThread => TelegramFilterKind.HasMessageThread,
            TelegramGeneratedFilterKind.HasCallbackData => TelegramFilterKind.HasCallbackData,
            TelegramGeneratedFilterKind.CallbackDataPrefix => TelegramFilterKind.CallbackDataPrefix,
            TelegramGeneratedFilterKind.SenderChatType => TelegramFilterKind.SenderChatType,
            TelegramGeneratedFilterKind.Custom => throw new InvalidOperationException("Custom generated Telegram filters are mapped separately."),
            _ => throw new InvalidOperationException($"Unsupported generated Telegram filter kind '{kind}'.")
        };
    }

    private static TelegramHandlerParameterKind MapParameterKind(TelegramGeneratedHandlerParameterKind kind)
    {
        return kind switch
        {
            TelegramGeneratedHandlerParameterKind.Context => TelegramHandlerParameterKind.Context,
            TelegramGeneratedHandlerParameterKind.CallbackPayload => TelegramHandlerParameterKind.CallbackPayload,
            TelegramGeneratedHandlerParameterKind.RouteValue => TelegramHandlerParameterKind.RouteValue,
            TelegramGeneratedHandlerParameterKind.Service => TelegramHandlerParameterKind.Service,
            TelegramGeneratedHandlerParameterKind.CancellationToken => TelegramHandlerParameterKind.CancellationToken,
            _ => throw new InvalidOperationException($"Unsupported generated Telegram handler parameter kind '{kind}'.")
        };
    }

    private static TelegramErrorHandlerParameterKind MapErrorParameterKind(TelegramGeneratedErrorHandlerParameterKind kind)
    {
        return kind switch
        {
            TelegramGeneratedErrorHandlerParameterKind.ErrorContext => TelegramErrorHandlerParameterKind.ErrorContext,
            TelegramGeneratedErrorHandlerParameterKind.TelegramContext => TelegramErrorHandlerParameterKind.TelegramContext,
            TelegramGeneratedErrorHandlerParameterKind.Exception => TelegramErrorHandlerParameterKind.Exception,
            TelegramGeneratedErrorHandlerParameterKind.RouteValue => TelegramErrorHandlerParameterKind.RouteValue,
            TelegramGeneratedErrorHandlerParameterKind.Service => TelegramErrorHandlerParameterKind.Service,
            TelegramGeneratedErrorHandlerParameterKind.CancellationToken => TelegramErrorHandlerParameterKind.CancellationToken,
            _ => throw new InvalidOperationException($"Unsupported generated Telegram error handler parameter kind '{kind}'.")
        };
    }
}
