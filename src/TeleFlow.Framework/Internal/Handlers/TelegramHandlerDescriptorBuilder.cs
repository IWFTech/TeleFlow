using System.Reflection;
using System.Text.RegularExpressions;
using TeleFlow.Annotations;
using TeleFlow.Framework.States;

namespace TeleFlow.Telegram.Internal.Handlers;

internal static class TelegramHandlerDescriptorBuilder
{
    private static readonly NullabilityInfoContext NullabilityInfoContext = new();

    public static IReadOnlyList<TelegramHandlerDescriptor> Build(
        Type handlerType,
        int firstRegistrationOrder)
    {
        ArgumentNullException.ThrowIfNull(handlerType);

        if (handlerType.IsAbstract || handlerType.IsInterface)
        {
            throw new InvalidOperationException(
                $"Telegram handler type '{handlerType.FullName}' must be a concrete class.");
        }

        var registrationOrder = firstRegistrationOrder;
        var descriptors = new List<TelegramHandlerDescriptor>();
        var moduleName = GetModuleName(handlerType);
        var isClassBasedHandler = IsClassBasedHandlerType(handlerType);

        if (!isClassBasedHandler && HasRouteAttributes(handlerType))
        {
            throw new InvalidOperationException(
                $"Telegram handler type '{handlerType.FullName}' declares class-level route metadata but does not derive from a TeleFlow class-based handler type.");
        }

        foreach (var method in GetCandidateMethods(handlerType, isClassBasedHandler))
        {
            var methodDescriptors = BuildForMethod(
                handlerType,
                method,
                registrationOrder,
                moduleName,
                includeClassRouteAttributes: isClassBasedHandler);

            descriptors.AddRange(methodDescriptors);
            registrationOrder += methodDescriptors.Count;
        }

        if (HasInheritedHandlerMethods(handlerType))
        {
            throw new InvalidOperationException(
                $"Telegram handler type '{handlerType.FullName}' inherits handler methods. " +
                "Inherited handler methods must be overridden in the concrete handler type for generated registration parity.");
        }

        return descriptors;
    }

    private static IEnumerable<MethodInfo> GetCandidateMethods(
        Type handlerType,
        bool isClassBasedHandler)
    {
        if (!isClassBasedHandler)
        {
            return GetDeclaredCandidateMethods(handlerType);
        }

        var handleMethods = GetDeclaredCandidateMethods(handlerType)
            .Where(static method => string.Equals(method.Name, "HandleAsync", StringComparison.Ordinal))
            .ToArray();

        if (handleMethods.Length != 1)
        {
            throw new InvalidOperationException(
                $"Class-based Telegram handler type '{handlerType.FullName}' must declare exactly one public instance HandleAsync method.");
        }

        var routeMethods = GetDeclaredCandidateMethods(handlerType)
            .Where(static method => !string.Equals(method.Name, "HandleAsync", StringComparison.Ordinal))
            .Where(HasRouteAttributes)
            .ToArray();

        if (routeMethods.Length > 0)
        {
            throw new InvalidOperationException(
                $"Class-based Telegram handler type '{handlerType.FullName}' must declare route metadata on the type or HandleAsync method only.");
        }

        return handleMethods;
    }

    private static IEnumerable<MethodInfo> GetDeclaredCandidateMethods(Type handlerType)
    {
        return handlerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .OrderBy(static method => method.MetadataToken);
    }

    private static bool HasInheritedHandlerMethods(Type handlerType)
    {
        return handlerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType != handlerType)
            .Any(HasRouteAttributes);
    }

    private static bool HasRouteAttributes(MemberInfo member)
    {
        return member.GetCustomAttributes<CommandAttribute>(inherit: true).Any() ||
               member.GetCustomAttributes<MessageAttribute>(inherit: true).Any() ||
               member.GetCustomAttributes<TextAttribute>(inherit: true).Any() ||
               member.GetCustomAttributes<TextTemplateAttribute>(inherit: true).Any() ||
               member.GetCustomAttributes<CommandTemplateAttribute>(inherit: true).Any() ||
               member.GetCustomAttributes<TextRegexAttribute>(inherit: true).Any() ||
               member.GetCustomAttributes<CommandRegexAttribute>(inherit: true).Any() ||
               member.GetCustomAttributes<CallbackAttribute>(inherit: true).Any() ||
               HasGenericCallbackAttribute(member) ||
               member.GetCustomAttributes<ChatMemberUpdatedAttribute>(inherit: true).Any() ||
               member.GetCustomAttributes<MyChatMemberUpdatedAttribute>(inherit: true).Any();
    }

    private static List<TelegramHandlerDescriptor> BuildForMethod(
        Type handlerType,
        MethodInfo method,
        int firstRegistrationOrder,
        string? moduleName,
        bool includeClassRouteAttributes = false)
    {
        var callback = GetRouteAttribute<CallbackAttribute>(handlerType, method, includeClassRouteAttributes);
        var callbackPayloadType = GetGenericCallbackPayloadType(handlerType, method, includeClassRouteAttributes);
        var hasCallbackRoute = callback is not null || callbackPayloadType is not null;
        var chatMemberUpdated = GetRouteAttribute<ChatMemberUpdatedAttribute>(handlerType, method, includeClassRouteAttributes);
        var myChatMemberUpdated = GetRouteAttribute<MyChatMemberUpdatedAttribute>(handlerType, method, includeClassRouteAttributes);
        var hasChatMemberRoute = chatMemberUpdated is not null || myChatMemberUpdated is not null;

        var commandAttributes = GetRouteAttributes<CommandAttribute>(handlerType, method, includeClassRouteAttributes);
        var message = GetRouteAttribute<MessageAttribute>(handlerType, method, includeClassRouteAttributes);
        var textAttributes = GetRouteAttributes<TextAttribute>(handlerType, method, includeClassRouteAttributes);
        var textTemplates = GetRouteAttributes<TextTemplateAttribute>(handlerType, method, includeClassRouteAttributes);
        var commandTemplates = GetRouteAttributes<CommandTemplateAttribute>(handlerType, method, includeClassRouteAttributes);
        var textRegexes = GetRouteAttributes<TextRegexAttribute>(handlerType, method, includeClassRouteAttributes);
        var commandRegexes = GetRouteAttributes<CommandRegexAttribute>(handlerType, method, includeClassRouteAttributes);
        var filters = BuildFilters(handlerType, method);
        var roleRequirements = BuildRoleRequirements(handlerType, method);
        var scene = handlerType.GetCustomAttribute<SceneAttribute>(inherit: false);
        var sceneStep = method.GetCustomAttribute<SceneStepAttribute>(inherit: true);

        var hasMessageRoute =
            commandAttributes.Length > 0 ||
            message is not null ||
            textAttributes.Length > 0 ||
            textTemplates.Length > 0 ||
            commandTemplates.Length > 0 ||
            textRegexes.Length > 0 ||
            commandRegexes.Length > 0;
        var autoAnswerCallback = BuildAutoAnswerCallback(handlerType, method, hasCallbackRoute);

        if (!hasCallbackRoute && !hasMessageRoute)
        {
            if (!hasChatMemberRoute)
            {
                if (sceneStep is not null)
                {
                    throw CreateSignatureException(
                        method,
                        "SceneStepAttribute requires an explicit Telegram route attribute.");
                }

                return [];
            }
        }

        if (sceneStep is not null && scene is null)
        {
            throw CreateSignatureException(
                method,
                "SceneStepAttribute can only be used on handler methods declared inside a SceneAttribute type.");
        }

        if (sceneStep is not null && HasStateAttributes(handlerType, method))
        {
            throw CreateSignatureException(
                method,
                "SceneStepAttribute cannot be mixed with StateAttribute or StateAttribute<TStateGroup>.");
        }

        if (callback is not null && callbackPayloadType is not null)
        {
            throw CreateSignatureException(
                method,
                "Raw CallbackAttribute and CallbackAttribute<TPayload> cannot be used on the same handler method.");
        }

        if (hasCallbackRoute && hasMessageRoute)
        {
            throw CreateSignatureException(
                method,
                "Callback routes cannot be mixed with message, text, command, template, or regex routes.");
        }

        if (hasChatMemberRoute && (hasCallbackRoute || hasMessageRoute))
        {
            throw CreateSignatureException(
                method,
                "Chat member update routes cannot be mixed with message, text, command, template, regex, or callback routes.");
        }

        if (!hasChatMemberRoute && HasChatMemberTransitionAttributes(handlerType, method))
        {
            throw CreateSignatureException(
                method,
                "Chat member transition attributes can only be used on chat member update handlers.");
        }

        if (hasCallbackRoute &&
            filters.Any(static filter => filter.Kind is not null &&
                                         !TelegramFilterCompatibility.Supports(filter.Kind.Value, TelegramHandlerKind.Callback)))
        {
            var invalidFilter = filters.First(filter => filter.Kind is not null &&
                                                        !TelegramFilterCompatibility.Supports(filter.Kind.Value, TelegramHandlerKind.Callback));
            throw CreateSignatureException(
                method,
                TelegramFilterCompatibility.GetInvalidPlacementMessage(invalidFilter.Kind!.Value, TelegramHandlerKind.Callback));
        }

        if (!hasCallbackRoute &&
            !hasChatMemberRoute &&
            filters.Any(static filter => filter.Kind is not null &&
                                         !TelegramFilterCompatibility.Supports(filter.Kind.Value, TelegramHandlerKind.Message)))
        {
            var invalidFilter = filters.First(filter => filter.Kind is not null &&
                                                        !TelegramFilterCompatibility.Supports(filter.Kind.Value, TelegramHandlerKind.Message));
            throw CreateSignatureException(
                method,
                TelegramFilterCompatibility.GetInvalidPlacementMessage(invalidFilter.Kind!.Value, TelegramHandlerKind.Message));
        }

        if (hasChatMemberRoute &&
            filters.Any(static filter => filter.Kind is not null &&
                                         !TelegramFilterCompatibility.Supports(filter.Kind.Value, TelegramHandlerKind.ChatMember)))
        {
            var invalidFilter = filters.First(filter => filter.Kind is not null &&
                                                        !TelegramFilterCompatibility.Supports(filter.Kind.Value, TelegramHandlerKind.ChatMember));
            throw CreateSignatureException(
                method,
                TelegramFilterCompatibility.GetInvalidPlacementMessage(invalidFilter.Kind!.Value, TelegramHandlerKind.ChatMember));
        }

        ValidateReturnType(method);
        ValidateCallbackPayloadType(method, callbackPayloadType);

        var states = TelegramStateAttributeResolver.GetStates(handlerType, method).ToList();

        if (sceneStep is not null)
        {
            states.Add(ResolveSceneStepState(handlerType, method, scene!, sceneStep));
        }

        List<RouteDefinition> routeDefinitions = hasChatMemberRoute
            ? BuildChatMemberRouteDefinitions(handlerType, method, chatMemberUpdated, myChatMemberUpdated)
            : hasCallbackRoute
                ? [CreateCallbackRouteDefinition(callbackPayloadType)]
                : BuildMessageRouteDefinitions(method, message, textAttributes, commandAttributes, textTemplates, commandTemplates, textRegexes, commandRegexes);

        var routeValues = ResolveRouteValueTypes(method, routeDefinitions);
        var kind = hasChatMemberRoute
            ? TelegramHandlerKind.ChatMember
            : hasCallbackRoute
                ? TelegramHandlerKind.Callback
                : TelegramHandlerKind.Message;

        ValidateClassBasedHandlerCompatibility(handlerType, method, kind, callbackPayloadType);

        ValidateCustomFilters(method, filters, kind);

        var parameters = BuildParameterDescriptors(method, kind, callbackPayloadType, routeValues);
        var descriptors = new List<TelegramHandlerDescriptor>(routeDefinitions.Count);

        for (var index = 0; index < routeDefinitions.Count; index++)
        {
            var definition = routeDefinitions[index];
            var route = new TelegramRouteDescriptor(
                definition.RouteKind,
                definition.Pattern,
                definition.CommandPolicy,
                definition.TextFilters,
                callbackPayloadType,
                routeValues
                    .Select(static value => value.Value)
                    .ToArray(),
                filters,
                definition.ChatMemberTransitions,
                roleRequirements);

            descriptors.Add(new TelegramHandlerDescriptor(
                handlerType,
                method,
                route,
                firstRegistrationOrder + index,
                moduleName,
                states,
                parameters,
                sceneStep is null ? null : scene!.Prefix,
                autoAnswerCallback));
        }

        return descriptors;
    }

    private static List<TelegramFilterDescriptor> BuildFilters(
        Type handlerType,
        MethodInfo method)
    {
        var filters = new List<TelegramFilterDescriptor>();

        AppendFilters(filters, handlerType);
        AppendFilters(filters, method);

        return filters;
    }

    private static TelegramRoleRequirementDescriptor[] BuildRoleRequirements(
        Type handlerType,
        MethodInfo method)
    {
        return handlerType
            .GetCustomAttributes<RequireTelegramRoleAttribute>(inherit: true)
            .Concat(method.GetCustomAttributes<RequireTelegramRoleAttribute>(inherit: true))
            .Select(static attribute => new TelegramRoleRequirementDescriptor(attribute.AllowedStatuses))
            .ToArray();
    }

    private static TelegramAutoAnswerCallbackDescriptor? BuildAutoAnswerCallback(
        Type handlerType,
        MethodInfo method,
        bool hasCallbackRoute)
    {
        var methodAttribute = method.GetCustomAttribute<AutoAnswerCallbackAttribute>(inherit: true);

        if (methodAttribute is not null && !hasCallbackRoute)
        {
            throw new InvalidOperationException(
                $"{nameof(AutoAnswerCallbackAttribute)} can be used only on callback handlers.");
        }

        if (!hasCallbackRoute)
        {
            return null;
        }

        var attribute = methodAttribute ??
            handlerType.GetCustomAttribute<AutoAnswerCallbackAttribute>(inherit: true);

        return attribute is null
            ? null
            : new TelegramAutoAnswerCallbackDescriptor(
                attribute.Enabled,
                NormalizeAutoAnswerText(attribute.Text),
                attribute.ShowAlert);
    }

    private static string? NormalizeAutoAnswerText(string? text)
    {
        if (text is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Auto callback answer text must not be empty.");
        }

        return text;
    }

    private static void AppendFilters(
        List<TelegramFilterDescriptor> filters,
        MemberInfo member)
    {
        var chatType = member.GetCustomAttribute<ChatTypeAttribute>(inherit: true);

        if (chatType is not null)
        {
            filters.Add(new TelegramFilterDescriptor(
                TelegramFilterKind.ChatType,
                chatType.ChatTypes.Select(TelegramFilterFacts.MapChatType).ToArray(),
                longValues: []));
        }

        var chatId = member.GetCustomAttribute<ChatIdAttribute>(inherit: true);

        if (chatId is not null)
        {
            filters.Add(new TelegramFilterDescriptor(
                TelegramFilterKind.ChatId,
                stringValues: [],
                chatId.ChatIds.ToArray()));
        }

        var chatUsername = member.GetCustomAttribute<ChatUsernameAttribute>(inherit: true);

        if (chatUsername is not null)
        {
            filters.Add(new TelegramFilterDescriptor(
                TelegramFilterKind.ChatUsername,
                chatUsername.Usernames.ToArray(),
                longValues: []));
        }

        var fromUser = member.GetCustomAttribute<FromUserAttribute>(inherit: true);

        if (fromUser is not null)
        {
            filters.Add(new TelegramFilterDescriptor(
                TelegramFilterKind.FromUser,
                stringValues: [],
                fromUser.UserIds.ToArray()));
        }

        AppendMarkerFilters(filters, member, TelegramMarkerFilterGroup.MessageContent);

        var fromBot = member.GetCustomAttribute<FromBotAttribute>(inherit: true);

        if (fromBot is not null)
        {
            filters.Add(new TelegramFilterDescriptor(
                TelegramFilterKind.FromBot,
                [fromBot.Value.ToString()],
                longValues: []));
        }

        AppendMarkerFilters(filters, member, TelegramMarkerFilterGroup.MessageSender);

        var messageThreadId = member.GetCustomAttribute<MessageThreadIdAttribute>(inherit: true);

        if (messageThreadId is not null)
        {
            filters.Add(new TelegramFilterDescriptor(
                TelegramFilterKind.MessageThreadId,
                stringValues: [],
                messageThreadId.MessageThreadIds.ToArray()));
        }

        AppendMarkerFilters(filters, member, TelegramMarkerFilterGroup.SharedMetadata);

        var callbackDataPrefix = member.GetCustomAttribute<CallbackDataPrefixAttribute>(inherit: true);

        if (callbackDataPrefix is not null)
        {
            filters.Add(new TelegramFilterDescriptor(
                TelegramFilterKind.CallbackDataPrefix,
                [callbackDataPrefix.Prefix],
                longValues: []));
        }

        foreach (var customFilter in GetCustomFilters(member))
        {
            filters.Add(customFilter);
        }
    }

    private static void AppendMarkerFilters(
        List<TelegramFilterDescriptor> filters,
        MemberInfo member,
        TelegramMarkerFilterGroup group)
    {
        foreach (var spec in TelegramFilterFacts.GetMarkerFilters(group))
        {
            if (TelegramFilterFacts.HasMarkerFilter(member, spec))
            {
                filters.Add(new TelegramFilterDescriptor(spec.Kind, stringValues: [], longValues: []));
            }
        }
    }

    private static IEnumerable<TelegramFilterDescriptor> GetCustomFilters(MemberInfo member)
    {
        foreach (var attribute in member.GetCustomAttributes(inherit: true))
        {
            var attributeType = attribute.GetType();

            if (attributeType.IsGenericType &&
                attributeType.GetGenericTypeDefinition() == typeof(UseFilterAttribute<>))
            {
                yield return new TelegramFilterDescriptor(attributeType.GetGenericArguments()[0]);
                continue;
            }

            if (TryGetTelegramFilterAttributeFilterType(attributeType, out var filterType))
            {
                yield return new TelegramFilterDescriptor(filterType, (Attribute)attribute);
            }
        }
    }

    private static bool TryGetTelegramFilterAttributeFilterType(
        Type attributeType,
        out Type filterType)
    {
        for (var current = attributeType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType &&
                current.GetGenericTypeDefinition() == typeof(TelegramFilterAttribute<>))
            {
                filterType = current.GetGenericArguments()[0];
                return true;
            }
        }

        filterType = null!;
        return false;
    }

    private static void ValidateCustomFilters(
        MethodInfo method,
        IReadOnlyList<TelegramFilterDescriptor> filters,
        TelegramHandlerKind kind)
    {
        foreach (var filter in filters.Where(static filter => filter.CustomFilterType is not null))
        {
            var filterType = filter.CustomFilterType!;

            if (filterType.IsInterface ||
                filterType.IsAbstract ||
                filterType.ContainsGenericParameters)
            {
                throw CreateSignatureException(
                    method,
                    $"Custom Telegram filter type '{filterType.FullName}' must be a concrete closed type.");
            }

            var expectedContextType = kind switch
            {
                TelegramHandlerKind.Callback => typeof(CallbackQueryContext),
                TelegramHandlerKind.ChatMember => typeof(ChatMemberUpdatedContext),
                _ => typeof(MessageContext)
            };

            if (filter.CustomFilterAttribute is null)
            {
                var contextTypes = GetCustomFilterContextTypes(filterType, attributeType: null);

                if (contextTypes.Length == 0)
                {
                    throw CreateSignatureException(
                        method,
                        $"Custom Telegram filter type '{filterType.FullName}' must implement ITelegramFilter<TContext>.");
                }

                if (!SupportsContextType(contextTypes, expectedContextType))
                {
                    throw CreateSignatureException(
                        method,
                        $"Custom Telegram filter type '{filterType.FullName}' is not compatible with {expectedContextType.Name} handlers.");
                }

                continue;
            }

            var attributeType = filter.CustomFilterAttribute.GetType();

            if (attributeType.IsGenericType)
            {
                throw CreateSignatureException(
                    method,
                    $"Custom Telegram filter attribute type '{attributeType.FullName}' must be non-generic.");
            }

            var typedContextTypes = GetCustomFilterContextTypes(filterType, attributeType);

            if (typedContextTypes.Length == 0)
            {
                throw CreateSignatureException(
                    method,
                    "Parameterized custom Telegram filter attributes require a filter type that implements ITelegramFilter<TContext, TAttribute>.");
            }

            if (!SupportsContextType(typedContextTypes, expectedContextType))
            {
                throw CreateSignatureException(
                    method,
                    $"Custom Telegram filter type '{filterType.FullName}' is not compatible with {expectedContextType.Name} handlers.");
            }
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

    private static string? GetModuleName(Type handlerType)
    {
        return handlerType.GetCustomAttribute<TelegramModuleAttribute>(inherit: false)?.Name;
    }

    private static bool IsClassBasedHandlerType(Type handlerType)
    {
        return typeof(MessageHandler).IsAssignableFrom(handlerType) ||
               typeof(CallbackHandler).IsAssignableFrom(handlerType) ||
               typeof(ChatMemberUpdateHandler).IsAssignableFrom(handlerType);
    }

    private static void ValidateClassBasedHandlerCompatibility(
        Type handlerType,
        MethodInfo method,
        TelegramHandlerKind kind,
        Type? callbackPayloadType)
    {
        if (!IsClassBasedHandlerType(handlerType))
        {
            return;
        }

        if (typeof(MessageHandler).IsAssignableFrom(handlerType))
        {
            if (kind is TelegramHandlerKind.Message or TelegramHandlerKind.Command)
            {
                return;
            }

            throw CreateSignatureException(
                method,
                $"Class-based handler type '{handlerType.FullName}' derives from {nameof(MessageHandler)} and can only use message, text, command, template, or regex routes.");
        }

        if (typeof(ChatMemberUpdateHandler).IsAssignableFrom(handlerType))
        {
            if (kind == TelegramHandlerKind.ChatMember)
            {
                return;
            }

            throw CreateSignatureException(
                method,
                $"Class-based handler type '{handlerType.FullName}' derives from {nameof(ChatMemberUpdateHandler)} and can only use chat-member update routes.");
        }

        if (typeof(CallbackHandler).IsAssignableFrom(handlerType))
        {
            if (kind != TelegramHandlerKind.Callback)
            {
                throw CreateSignatureException(
                    method,
                    $"Class-based handler type '{handlerType.FullName}' derives from {nameof(CallbackHandler)} and can only use callback routes.");
            }

            var basePayloadType = GetCallbackHandlerPayloadType(handlerType);

            if (basePayloadType is null && callbackPayloadType is null)
            {
                return;
            }

            if (basePayloadType is not null &&
                callbackPayloadType is not null &&
                basePayloadType == callbackPayloadType)
            {
                return;
            }

            throw CreateSignatureException(
                method,
                $"Class-based typed callback handler '{handlerType.FullName}' must use matching CallbackAttribute<TPayload> metadata and CallbackHandler<TPayload> base type.");
        }
    }

    private static Type? GetCallbackHandlerPayloadType(Type handlerType)
    {
        for (var current = handlerType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType &&
                current.GetGenericTypeDefinition() == typeof(CallbackHandler<>))
            {
                return current.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static TAttribute? GetRouteAttribute<TAttribute>(
        Type handlerType,
        MethodInfo method,
        bool includeClassRouteAttributes)
        where TAttribute : Attribute
    {
        var attributes = GetRouteAttributes<TAttribute>(handlerType, method, includeClassRouteAttributes);
        return attributes.Length > 0 ? attributes[0] : null;
    }

    private static TAttribute[] GetRouteAttributes<TAttribute>(
        Type handlerType,
        MethodInfo method,
        bool includeClassRouteAttributes)
        where TAttribute : Attribute
    {
        var methodAttributes = method.GetCustomAttributes<TAttribute>(inherit: true);

        return includeClassRouteAttributes
            ? handlerType.GetCustomAttributes<TAttribute>(inherit: true)
                .Concat(methodAttributes)
                .ToArray()
            : methodAttributes.ToArray();
    }

    private static List<RouteDefinition> BuildMessageRouteDefinitions(
        MethodInfo method,
        MessageAttribute? message,
        IReadOnlyList<TextAttribute> textAttributes,
        IReadOnlyList<CommandAttribute> commandAttributes,
        IReadOnlyList<TextTemplateAttribute> textTemplates,
        IReadOnlyList<CommandTemplateAttribute> commandTemplates,
        IReadOnlyList<TextRegexAttribute> textRegexes,
        IReadOnlyList<CommandRegexAttribute> commandRegexes)
    {
        var definitions = new List<RouteDefinition>();

        foreach (var command in commandAttributes)
        {
            var policy = CreateCommandPolicy(command.Prefixes, command.AllowSpaceAfterPrefix, command.IgnoreCase, command.PrefixMode);
            definitions.Add(new RouteDefinition(
                TelegramRouteKind.CommandExact,
                NormalizeCommand(method, command.Command),
                policy,
                TextFilters: [],
                RouteValues: CreateEmptyRouteValues(),
                ChatMemberTransitions: []));
        }

        foreach (var template in commandTemplates)
        {
            var policy = CreateCommandPolicy(template.Prefixes, template.AllowSpaceAfterPrefix, template.IgnoreCase, template.PrefixMode);
            definitions.Add(new RouteDefinition(
                TelegramRouteKind.CommandTemplate,
                NormalizeCommandPattern(method, template.Template, policy),
                policy,
                TextFilters: [],
                TelegramTemplateRouteParser.GetRouteValues(template.Template)
                    .ToDictionary(
                        static value => value.Name,
                        static value => new RouteValueDefinition(value.ValueType, value.IsOptional),
                        StringComparer.Ordinal),
                ChatMemberTransitions: []));
        }

        foreach (var regex in commandRegexes)
        {
            var policy = CreateCommandPolicy(regex.Prefixes, regex.AllowSpaceAfterPrefix, regex.IgnoreCase, regex.PrefixMode);
            var routeValues = GetRegexRouteValueNames(method, regex.Pattern, regex.IgnoreCase)
                .ToDictionary(
                    static name => name,
                    static _ => new RouteValueDefinition(ValueType: null, IsOptional: false),
                    StringComparer.Ordinal);
            definitions.Add(new RouteDefinition(
                TelegramRouteKind.CommandRegex,
                regex.Pattern,
                policy,
                TextFilters: [],
                routeValues,
                ChatMemberTransitions: []));
        }

        if (message is not null)
        {
            definitions.Add(new RouteDefinition(
                TelegramRouteKind.MessageAny,
                Pattern: null,
                CommandPolicy: null,
                textAttributes
                    .Select(static attribute => new TelegramTextFilter(attribute.Value, attribute.Mode, attribute.IgnoreCase))
                    .ToArray(),
                RouteValues: CreateEmptyRouteValues(),
                ChatMemberTransitions: []));
        }
        else
        {
            foreach (var text in textAttributes)
            {
                definitions.Add(new RouteDefinition(
                    TelegramRouteKind.TextExact,
                    text.Value,
                    CommandPolicy: null,
                    [new TelegramTextFilter(text.Value, text.Mode, text.IgnoreCase)],
                    RouteValues: CreateEmptyRouteValues(),
                    ChatMemberTransitions: []));
            }
        }

        foreach (var template in textTemplates)
        {
            definitions.Add(new RouteDefinition(
                TelegramRouteKind.TextTemplate,
                template.Template,
                CommandPolicy: new TelegramCommandPolicy(["/"], allowSpaceAfterPrefix: false, template.IgnoreCase),
                TextFilters: [],
                TelegramTemplateRouteParser.GetRouteValues(template.Template)
                    .ToDictionary(
                        static value => value.Name,
                        static value => new RouteValueDefinition(value.ValueType, value.IsOptional),
                        StringComparer.Ordinal),
                ChatMemberTransitions: []));
        }

        foreach (var regex in textRegexes)
        {
            var routeValues = GetRegexRouteValueNames(method, regex.Pattern, regex.IgnoreCase)
                .ToDictionary(
                    static name => name,
                    static _ => new RouteValueDefinition(ValueType: null, IsOptional: false),
                    StringComparer.Ordinal);
            definitions.Add(new RouteDefinition(
                TelegramRouteKind.TextRegex,
                regex.Pattern,
                CommandPolicy: new TelegramCommandPolicy(["/"], allowSpaceAfterPrefix: false, regex.IgnoreCase),
                TextFilters: [],
                routeValues,
                ChatMemberTransitions: []));
        }

        return definitions;
    }

    private static RouteDefinition CreateCallbackRouteDefinition(Type? callbackPayloadType)
    {
        return new RouteDefinition(
            TelegramRouteKind.Callback,
            Pattern: null,
            CommandPolicy: null,
            TextFilters: [],
            RouteValues: CreateEmptyRouteValues(),
            ChatMemberTransitions: []);
    }

    private static List<RouteDefinition> BuildChatMemberRouteDefinitions(
        Type handlerType,
        MethodInfo method,
        ChatMemberUpdatedAttribute? chatMemberUpdated,
        MyChatMemberUpdatedAttribute? myChatMemberUpdated)
    {
        var transitions = BuildChatMemberTransitions(handlerType, method);
        var definitions = new List<RouteDefinition>(capacity: 2);

        if (chatMemberUpdated is not null)
        {
            definitions.Add(new RouteDefinition(
                TelegramRouteKind.ChatMemberUpdated,
                Pattern: null,
                CommandPolicy: null,
                TextFilters: [],
                RouteValues: CreateEmptyRouteValues(),
                transitions));
        }

        if (myChatMemberUpdated is not null)
        {
            definitions.Add(new RouteDefinition(
                TelegramRouteKind.MyChatMemberUpdated,
                Pattern: null,
                CommandPolicy: null,
                TextFilters: [],
                RouteValues: CreateEmptyRouteValues(),
                transitions));
        }

        return definitions;
    }

    private static TelegramChatMemberTransitionDescriptor[] BuildChatMemberTransitions(
        Type handlerType,
        MethodInfo method)
    {
        return GetChatMemberTransitionAttributes(handlerType, method)
            .Select(static attribute => TelegramMemberTransitionFacts.Map(attribute.Transition))
            .Concat(GetChatMemberChangedAttributes(handlerType, method)
                .Select(static attribute => new TelegramChatMemberTransitionDescriptor(
                    attribute.OldStatus,
                    attribute.NewStatus)))
            .ToArray();
    }

    private static IEnumerable<ChatMemberTransitionAttribute> GetChatMemberTransitionAttributes(
        Type handlerType,
        MethodInfo method)
    {
        return handlerType
            .GetCustomAttributes<ChatMemberTransitionAttribute>(inherit: true)
            .Concat(method.GetCustomAttributes<ChatMemberTransitionAttribute>(inherit: true));
    }

    private static IEnumerable<ChatMemberChangedAttribute> GetChatMemberChangedAttributes(
        Type handlerType,
        MethodInfo method)
    {
        return handlerType
            .GetCustomAttributes<ChatMemberChangedAttribute>(inherit: true)
            .Concat(method.GetCustomAttributes<ChatMemberChangedAttribute>(inherit: true));
    }

    private static bool HasChatMemberTransitionAttributes(
        Type handlerType,
        MethodInfo method)
    {
        return GetChatMemberTransitionAttributes(handlerType, method).Any() ||
               GetChatMemberChangedAttributes(handlerType, method).Any();
    }

    private static bool HasStateAttributes(
        Type handlerType,
        MethodInfo method)
    {
        return handlerType.GetCustomAttributes<StateAttribute>(inherit: true).Any() ||
               method.GetCustomAttributes<StateAttribute>(inherit: true).Any() ||
               HasGenericStateAttributes(handlerType) ||
               HasGenericStateAttributes(method);
    }

    private static bool HasGenericStateAttributes(MemberInfo member)
    {
        return member
            .GetCustomAttributes(inherit: true)
            .Select(static attribute => attribute.GetType())
            .Any(static type =>
                type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(StateAttribute<>));
    }

    private static string ResolveSceneStepState(
        Type handlerType,
        MethodInfo method,
        SceneAttribute scene,
        SceneStepAttribute step)
    {
        var property = handlerType.GetProperty(
            step.StateName,
            BindingFlags.Public | BindingFlags.Static);

        if (property is null || property.PropertyType != typeof(State))
        {
            throw CreateSignatureException(
                method,
                $"SceneStepAttribute references missing scene state '{handlerType.FullName}.{step.StateName}'.");
        }

        if (property.GetValue(null) is not State state || string.IsNullOrWhiteSpace(state.Id))
        {
            throw CreateSignatureException(
                method,
                $"Scene state '{handlerType.FullName}.{step.StateName}' must return a non-empty State value.");
        }

        var expectedStateId = $"{scene.Prefix}:{GetSceneStateSegment(property)}";

        if (!string.Equals(state.Id, expectedStateId, StringComparison.Ordinal))
        {
            throw CreateSignatureException(
                method,
                $"Scene state '{handlerType.FullName}.{step.StateName}' must return canonical state id '{expectedStateId}'.");
        }

        return state.Id;
    }

    private static string GetSceneStateSegment(PropertyInfo property)
    {
        var attribute = property.GetCustomAttribute<StateValueAttribute>(inherit: false);

        return attribute is not null && !string.IsNullOrWhiteSpace(attribute.Value)
            ? attribute.Value
            : ToCamelCase(property.Name);
    }

    private static string ToCamelCase(string value)
    {
        return value.Length == 0
            ? value
            : char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static Dictionary<string, TelegramRouteValueDescriptor> ResolveRouteValueTypes(
        MethodInfo method,
        IReadOnlyList<RouteDefinition> routeDefinitions)
    {
        var routeValueDefinitions = routeDefinitions
            .Select(static definition => definition.RouteValues)
            .Where(static values => values.Count > 0)
            .ToArray();

        if (routeValueDefinitions.Length == 0)
        {
            return new Dictionary<string, TelegramRouteValueDescriptor>(StringComparer.Ordinal);
        }

        var firstNames = routeValueDefinitions[0].Keys.Order(StringComparer.Ordinal).ToArray();

        foreach (var values in routeValueDefinitions.Skip(1))
        {
            var names = values.Keys.Order(StringComparer.Ordinal).ToArray();

            if (!firstNames.SequenceEqual(names, StringComparer.Ordinal))
            {
                throw CreateSignatureException(
                    method,
                    "Multiple route attributes on one handler method must expose the same route value names.");
            }
        }

        if (routeDefinitions.Any(static definition => definition.RouteValues.Count == 0))
        {
            throw CreateSignatureException(
                method,
                "A handler method with route value parameters cannot also have routes that do not provide route values.");
        }

        var parameters = method.GetParameters();
        var routeValues = new Dictionary<string, TelegramRouteValueDescriptor>(StringComparer.Ordinal);

        foreach (var name in firstNames)
        {
            var parameter = parameters.FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal));

            if (parameter is null)
            {
                throw CreateSignatureException(
                    method,
                    $"Route value '{name}' must have a matching handler parameter with the same name.");
            }

            if (!TryGetRouteValueParameterShape(parameter, out var parameterValueType, out var parameterIsNullable))
            {
                throw CreateSignatureException(
                    method,
                    $"Route value parameter '{name}' must be string, string?, int, int?, long, or long?.");
            }

            foreach (var values in routeValueDefinitions)
            {
                var definition = values[name];

                if (definition.IsOptional != parameterIsNullable)
                {
                    var expectedNullability = definition.IsOptional ? "nullable" : "non-nullable";
                    throw CreateSignatureException(
                        method,
                        $"Route value parameter '{name}' must be {expectedNullability} for the selected route template.");
                }

                if (definition.ValueType is { } expectedType && expectedType != parameterValueType)
                {
                    throw CreateSignatureException(
                        method,
                        $"Route value parameter '{name}' must be {expectedType.Name} for the selected route template.");
                }
            }

            routeValues.Add(name, new TelegramRouteValueDescriptor(name, parameterValueType, parameterIsNullable));
        }

        return routeValues;
    }

    private static List<TelegramHandlerParameterDescriptor> BuildParameterDescriptors(
        MethodInfo method,
        TelegramHandlerKind kind,
        Type? callbackPayloadType,
        Dictionary<string, TelegramRouteValueDescriptor> routeValues)
    {
        var expectedContextType = kind switch
        {
            TelegramHandlerKind.Callback => typeof(CallbackQueryContext),
            TelegramHandlerKind.ChatMember => typeof(ChatMemberUpdatedContext),
            _ => typeof(MessageContext)
        };

        var parameters = method.GetParameters();
        var contextParameters = parameters
            .Where(parameter => parameter.ParameterType == typeof(MessageContext) ||
                                parameter.ParameterType == typeof(CallbackQueryContext) ||
                                parameter.ParameterType == typeof(ChatMemberUpdatedContext) ||
                                parameter.ParameterType == typeof(TelegramUpdateContext))
            .ToArray();

        if (contextParameters.Length != 1 || contextParameters[0].ParameterType != expectedContextType)
        {
            throw CreateSignatureException(
                method,
                $"A {FormatHandlerKind(kind)} handler must declare exactly one {expectedContextType.Name} parameter.");
        }

        if (parameters.Count(static parameter => parameter.ParameterType == typeof(CancellationToken)) > 1)
        {
            throw CreateSignatureException(method, "A handler method can declare at most one CancellationToken parameter.");
        }

        var descriptors = new List<TelegramHandlerParameterDescriptor>(parameters.Length);

        foreach (var parameter in parameters)
        {
            if (parameter.ParameterType == expectedContextType)
            {
                descriptors.Add(new TelegramHandlerParameterDescriptor(parameter, TelegramHandlerParameterKind.Context));
                continue;
            }

            if (parameter.ParameterType == typeof(CancellationToken))
            {
                descriptors.Add(new TelegramHandlerParameterDescriptor(parameter, TelegramHandlerParameterKind.CancellationToken));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(parameter.Name) && routeValues.ContainsKey(parameter.Name))
            {
                descriptors.Add(new TelegramHandlerParameterDescriptor(parameter, TelegramHandlerParameterKind.RouteValue));
                continue;
            }

            if (kind == TelegramHandlerKind.Callback && callbackPayloadType == parameter.ParameterType)
            {
                descriptors.Add(new TelegramHandlerParameterDescriptor(parameter, TelegramHandlerParameterKind.CallbackPayload));
                continue;
            }

            if (kind == TelegramHandlerKind.Callback &&
                callbackPayloadType is null &&
                IsCallbackDataPayloadType(parameter.ParameterType))
            {
                throw CreateSignatureException(
                    method,
                    $"Raw {nameof(CallbackAttribute)} handlers do not bind typed callback payloads. Use {nameof(CallbackAttribute)}<TPayload>.");
            }

            descriptors.Add(new TelegramHandlerParameterDescriptor(parameter, TelegramHandlerParameterKind.Service));
        }

        if (kind == TelegramHandlerKind.Callback && callbackPayloadType is not null)
        {
            var payloadParameterCount = parameters.Count(parameter => parameter.ParameterType == callbackPayloadType);

            if (payloadParameterCount != 1)
            {
                throw CreateSignatureException(
                    method,
                    $"{nameof(CallbackAttribute)}<{callbackPayloadType.Name}> handlers must declare exactly one {callbackPayloadType.Name} payload parameter.");
            }
        }

        return descriptors;
    }

    private static TelegramCommandPolicy CreateCommandPolicy(
        IReadOnlyList<string> prefixes,
        bool allowSpaceAfterPrefix,
        bool ignoreCase,
        CommandPrefixMode prefixMode)
    {
        return new TelegramCommandPolicy(prefixes, allowSpaceAfterPrefix, ignoreCase, prefixMode);
    }

    private static Dictionary<string, RouteValueDefinition> CreateEmptyRouteValues()
    {
        return new Dictionary<string, RouteValueDefinition>(StringComparer.Ordinal);
    }

    private static string[] GetRegexRouteValueNames(
        MethodInfo method,
        string pattern,
        bool ignoreCase)
    {
        Regex regex;

        try
        {
            regex = new Regex(pattern, TelegramTemplateRouteParser.GetRegexOptions(ignoreCase));
        }
        catch (ArgumentException exception)
        {
            throw CreateSignatureException(method, $"Regex route pattern is invalid: {exception.Message}");
        }

        return regex
            .GetGroupNames()
            .Where(static name => !int.TryParse(name, out _))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static Type? GetGenericCallbackPayloadType(
        Type handlerType,
        MethodInfo method,
        bool includeClassRouteAttributes)
    {
        var attributes = method
            .GetCustomAttributes(inherit: true)
            .Select(static attribute => attribute.GetType())
            .Where(static type =>
                type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(CallbackAttribute<>));

        if (includeClassRouteAttributes)
        {
            attributes = handlerType
                .GetCustomAttributes(inherit: true)
                .Select(static attribute => attribute.GetType())
                .Where(static type =>
                    type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(CallbackAttribute<>))
                .Concat(attributes);
        }

        var payloadTypes = attributes
            .Select(static type => type.GetGenericArguments()[0])
            .Distinct()
            .ToArray();

        if (payloadTypes.Length > 1)
        {
            throw CreateSignatureException(
                method,
                "CallbackAttribute<TPayload> metadata on one handler must resolve to exactly one payload type.");
        }

        return payloadTypes.FirstOrDefault();
    }

    private static bool HasGenericCallbackAttribute(MemberInfo member)
    {
        return member
            .GetCustomAttributes(inherit: true)
            .Select(static attribute => attribute.GetType())
            .Any(static type =>
                type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(CallbackAttribute<>));
    }

    private static void ValidateCallbackPayloadType(MethodInfo method, Type? callbackPayloadType)
    {
        if (callbackPayloadType is null)
        {
            return;
        }

        if (callbackPayloadType.IsInterface ||
            callbackPayloadType.IsAbstract ||
            callbackPayloadType.ContainsGenericParameters)
        {
            throw CreateSignatureException(
                method,
                $"{nameof(CallbackAttribute)} payload type '{callbackPayloadType.FullName}' must be a concrete closed type.");
        }
    }

    private static bool IsCallbackDataPayloadType(Type type)
    {
        return type.GetCustomAttribute<CallbackDataAttribute>(inherit: false) is not null;
    }

    private static void ValidateReturnType(MethodInfo method)
    {
        if (method.ReturnType != typeof(Task) && method.ReturnType != typeof(ValueTask))
        {
            throw CreateSignatureException(
                method,
                "A Telegram handler method must return Task or ValueTask.");
        }
    }

    private static string NormalizeCommand(MethodInfo method, string command)
    {
        var normalized = command.Trim();

        if (normalized.StartsWith('/') ||
            normalized.Contains('@', StringComparison.Ordinal) ||
            normalized.Any(char.IsWhiteSpace))
        {
            throw CreateSignatureException(
                method,
                "CommandAttribute value must be a command name without '/', bot username, or whitespace.");
        }

        return normalized;
    }

    private static string FormatHandlerKind(TelegramHandlerKind kind)
    {
        return kind switch
        {
            TelegramHandlerKind.Command => "command",
            TelegramHandlerKind.Message => "message",
            TelegramHandlerKind.Callback => "callback",
            TelegramHandlerKind.ChatMember => "chat member update",
            _ => kind.ToString()
        };
    }

    private static string NormalizeCommandPattern(
        MethodInfo method,
        string pattern,
        TelegramCommandPolicy policy)
    {
        var normalized = pattern.Trim();

        if (policy.Prefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal)))
        {
            throw CreateSignatureException(
                method,
                "Command route template value must not include a command prefix.");
        }

        return normalized;
    }

    private static bool TryGetRouteValueParameterShape(
        ParameterInfo parameter,
        out Type valueType,
        out bool isNullable)
    {
        var parameterType = parameter.ParameterType;
        var underlyingType = Nullable.GetUnderlyingType(parameterType);

        if (underlyingType is not null)
        {
            valueType = underlyingType;
            isNullable = true;
            return valueType == typeof(int) || valueType == typeof(long);
        }

        if (parameterType == typeof(string))
        {
            valueType = typeof(string);
            isNullable = NullabilityInfoContext.Create(parameter).ReadState == NullabilityState.Nullable;
            return true;
        }

        valueType = parameterType;
        isNullable = false;

        return valueType == typeof(int) || valueType == typeof(long);
    }

    private static InvalidOperationException CreateSignatureException(MethodInfo method, string reason)
    {
        return new InvalidOperationException(
            $"Invalid Telegram handler signature '{method.DeclaringType?.FullName}.{method.Name}': {reason}");
    }

    private sealed record RouteDefinition(
        TelegramRouteKind RouteKind,
        string? Pattern,
        TelegramCommandPolicy? CommandPolicy,
        IReadOnlyList<TelegramTextFilter> TextFilters,
        IReadOnlyDictionary<string, RouteValueDefinition> RouteValues,
        IReadOnlyList<TelegramChatMemberTransitionDescriptor> ChatMemberTransitions);

    private readonly record struct RouteValueDefinition(
        Type? ValueType,
        bool IsOptional);
}
