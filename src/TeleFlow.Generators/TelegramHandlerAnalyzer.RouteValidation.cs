using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TeleFlow.Generators;

public sealed partial class TelegramHandlerAnalyzer
{
    private const int CommandPrefixModeRequired = 0;
    private const int CommandPrefixModeOptional = 1;
    private const int CommandPrefixModeNoPrefix = 2;

    private static HandlerKind? GetRouteKind(
        IMethodSymbol method,
        bool includeClassRouteAttributes,
        out ITypeSymbol? callbackPayloadType,
        out bool hasRawCallback,
        out bool hasMixedCallbackRoutes)
    {
        callbackPayloadType = null;
        hasRawCallback = GetRouteAttributes(
            method,
            TelegramHandlerSymbols.CallbackAttribute,
            includeClassRouteAttributes).Count > 0;

        IReadOnlyList<AttributeData> genericCallbackAttributes = GetGenericRouteAttributes(
            method,
            TelegramHandlerSymbols.GenericCallbackAttribute,
            includeClassRouteAttributes);
        AttributeData? genericCallback = genericCallbackAttributes.Count > 0
            ? genericCallbackAttributes[0]
            : null;

        if (genericCallback?.AttributeClass is INamedTypeSymbol { TypeArguments.Length: 1 } genericCallbackType)
        {
            callbackPayloadType = genericCallbackType.TypeArguments[0];
        }

        bool hasCallback = hasRawCallback || callbackPayloadType is not null;
        int messageRouteCount = GetMessageRouteAttributeCount(method, includeClassRouteAttributes);
        int chatMemberRouteCount = GetChatMemberRouteAttributeCount(method, includeClassRouteAttributes);
        hasMixedCallbackRoutes = (hasRawCallback && callbackPayloadType is not null) ||
                                 (hasCallback && (messageRouteCount > 0 || chatMemberRouteCount > 0)) ||
                                 (chatMemberRouteCount > 0 && messageRouteCount > 0);

        if (hasMixedCallbackRoutes)
        {
            return null;
        }

        if (hasCallback)
        {
            return HandlerKind.Callback;
        }

        if (chatMemberRouteCount > 0)
        {
            return HandlerKind.ChatMember;
        }

        return messageRouteCount > 0 ? HandlerKind.Message : null;
    }

    private static int GetMessageRouteAttributeCount(
        IMethodSymbol method,
        bool includeClassRouteAttributes = false)
    {
        return GetRouteAttributes(method, TelegramHandlerSymbols.CommandAttribute, includeClassRouteAttributes).Count +
               GetRouteAttributes(method, TelegramHandlerSymbols.MessageAttribute, includeClassRouteAttributes).Count +
               GetRouteAttributes(method, TelegramHandlerSymbols.TextAttribute, includeClassRouteAttributes).Count +
               GetRouteAttributes(method, TelegramHandlerSymbols.TextTemplateAttribute, includeClassRouteAttributes).Count +
               GetRouteAttributes(method, TelegramHandlerSymbols.CommandTemplateAttribute, includeClassRouteAttributes).Count +
               GetRouteAttributes(method, TelegramHandlerSymbols.TextRegexAttribute, includeClassRouteAttributes).Count +
               GetRouteAttributes(method, TelegramHandlerSymbols.CommandRegexAttribute, includeClassRouteAttributes).Count;
    }

    private static bool HasAnyMessageRouteAttribute(
        IMethodSymbol method,
        bool includeClassRouteAttributes = false)
    {
        return GetMessageRouteAttributeCount(method, includeClassRouteAttributes) > 0;
    }

    private static int GetChatMemberRouteAttributeCount(
        IMethodSymbol method,
        bool includeClassRouteAttributes = false)
    {
        return GetRouteAttributes(method, TelegramHandlerSymbols.ChatMemberUpdatedAttribute, includeClassRouteAttributes).Count +
               GetRouteAttributes(method, TelegramHandlerSymbols.MyChatMemberUpdatedAttribute, includeClassRouteAttributes).Count;
    }

    private static void AnalyzeCommandRoutes(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        ConcurrentBag<CommandRegistration> commands,
        string displayName,
        Location? location,
        bool includeClassRouteAttributes = false)
    {
        foreach (AttributeData attribute in GetRouteAttributes(
                     method,
                     TelegramHandlerSymbols.CommandAttribute,
                     includeClassRouteAttributes))
        {
            string? command = GetConstructorString(attribute);

            if (!IsValidCommand(command))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidCommand,
                    location,
                    displayName));
                continue;
            }

            if (!TryGetPrefixes(attribute, out IReadOnlyList<string> prefixes, out string prefixReason))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidCommandPrefix,
                    location,
                    prefixReason));
                continue;
            }

            int prefixMode = GetCommandPrefixMode(attribute);

            if (!TryValidateCommandPrefixMode(attribute, prefixMode, out string prefixModeReason))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidCommandPrefix,
                    location,
                    prefixModeReason));
                continue;
            }

            bool allowSpace = GetNamedBool(attribute, "AllowSpaceAfterPrefix", defaultValue: false);

            if (prefixMode is CommandPrefixModeRequired or CommandPrefixModeOptional)
            {
                foreach (string prefix in prefixes)
                {
                    commands.Add(new CommandRegistration(
                        $"{prefix.ToUpperInvariant()}\u001F{allowSpace}\u001FPREFIXED\u001F{command!.ToUpperInvariant()}",
                        $"{prefix}{command}",
                        location));
                }
            }

            if (prefixMode is CommandPrefixModeOptional or CommandPrefixModeNoPrefix)
            {
                commands.Add(new CommandRegistration(
                    $"NO_PREFIX\u001F{command!.ToUpperInvariant()}",
                    command,
                    location));
            }
        }
    }

    private static void AnalyzeTemplateAndRegexRoutes(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        HandlerKind routeKind,
        Location? location,
        bool includeClassRouteAttributes = false)
    {
        if (routeKind is HandlerKind.Callback or HandlerKind.ChatMember)
        {
            return;
        }

        List<IReadOnlyDictionary<string, RouteValueDefinition>> routeValues = new List<IReadOnlyDictionary<string, RouteValueDefinition>>();

        foreach (AttributeData attribute in GetRouteAttributes(
                     method,
                     TelegramHandlerSymbols.TextTemplateAttribute,
                     includeClassRouteAttributes))
        {
            string? template = GetConstructorString(attribute);

            if (!TryParseRouteTemplate(template, out IReadOnlyDictionary<string, RouteValueDefinition> values, out string reason))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidRouteTemplate,
                    location,
                    reason));
                continue;
            }

            routeValues.Add(values);
        }

        foreach (AttributeData attribute in GetRouteAttributes(
                     method,
                     TelegramHandlerSymbols.CommandTemplateAttribute,
                     includeClassRouteAttributes))
        {
            string? template = GetConstructorString(attribute);

            if (!TryParseRouteTemplate(template, out IReadOnlyDictionary<string, RouteValueDefinition> values, out string reason))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidRouteTemplate,
                    location,
                    reason));
                continue;
            }

            if (!TryGetPrefixes(attribute, out IReadOnlyList<string> prefixes, out string prefixReason))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidCommandPrefix,
                    location,
                    prefixReason));
                continue;
            }

            int prefixMode = GetCommandPrefixMode(attribute);

            if (!TryValidateCommandPrefixMode(attribute, prefixMode, out string prefixModeReason))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidCommandPrefix,
                    location,
                    prefixModeReason));
                continue;
            }

            if (CommandPatternStartsWithPrefix(template!, prefixes))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidRouteTemplate,
                    location,
                    "Command route template value must not include a command prefix."));
                continue;
            }

            routeValues.Add(values);
        }

        foreach (AttributeData attribute in GetRouteAttributes(
                     method,
                     TelegramHandlerSymbols.TextRegexAttribute,
                     includeClassRouteAttributes))
        {
            string? pattern = GetConstructorString(attribute);

            if (!TryParseRouteRegex(pattern, out IReadOnlyDictionary<string, RouteValueDefinition> values, out string reason))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidRouteRegex,
                    location,
                    reason));
                continue;
            }

            routeValues.Add(values);
        }

        foreach (AttributeData attribute in GetRouteAttributes(
                     method,
                     TelegramHandlerSymbols.CommandRegexAttribute,
                     includeClassRouteAttributes))
        {
            string? pattern = GetConstructorString(attribute);

            if (!TryParseRouteRegex(pattern, out IReadOnlyDictionary<string, RouteValueDefinition> values, out string reason))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidRouteRegex,
                    location,
                    reason));
                continue;
            }

            if (!TryGetPrefixes(attribute, out _, out string prefixReason))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidCommandPrefix,
                    location,
                    prefixReason));
                continue;
            }

            int prefixMode = GetCommandPrefixMode(attribute);

            if (!TryValidateCommandPrefixMode(attribute, prefixMode, out string prefixModeReason))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidCommandPrefix,
                    location,
                    prefixModeReason));
                continue;
            }

            routeValues.Add(values);
        }

        ValidateRouteValueParameters(context, method, routeValues, location, includeClassRouteAttributes);
    }

    private static void ValidateRouteValueParameters(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        IReadOnlyList<IReadOnlyDictionary<string, RouteValueDefinition>> routeValues,
        Location? location,
        bool includeClassRouteAttributes = false)
    {
        if (routeValues.Count == 0)
        {
            return;
        }

        string[] firstNames = routeValues[0].Keys.OrderBy(static name => name, StringComparer.Ordinal).ToArray();

        if (GetMessageRouteAttributeCount(method, includeClassRouteAttributes) > routeValues.Count)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidRouteValue,
                location,
                "A handler method with route value parameters cannot also have routes that do not provide route values."));
            return;
        }

        foreach (IReadOnlyDictionary<string, RouteValueDefinition> values in routeValues.Skip(1))
        {
            string[] names = values.Keys.OrderBy(static name => name, StringComparer.Ordinal).ToArray();

            if (!firstNames.SequenceEqual(names, StringComparer.Ordinal))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidRouteValue,
                    location,
                    "Multiple route attributes on one handler method must expose the same route value names."));
                return;
            }
        }

        foreach (string name in firstNames)
        {
            IParameterSymbol? parameter = method.Parameters.FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal));

            if (parameter is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidRouteValue,
                    location,
                    $"Route value '{name}' must have a matching handler parameter with the same name."));
                continue;
            }

            if (!IsSupportedRouteValueParameterType(parameter.Type))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidRouteValue,
                    parameter.Locations.FirstOrDefault(static candidate => candidate.IsInSource) ?? location,
                    $"Route value parameter '{name}' must be string, string?, int, int?, long, or long?."));
                continue;
            }

            foreach (IReadOnlyDictionary<string, RouteValueDefinition> values in routeValues)
            {
                RouteValueDefinition routeValue = values[name];

                if (routeValue.IsOptional != IsNullableRouteValueParameter(parameter.Type))
                {
                    string expectedNullability = routeValue.IsOptional ? "nullable" : "non-nullable";
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidRouteValue,
                        parameter.Locations.FirstOrDefault(static candidate => candidate.IsInSource) ?? location,
                        $"Route value parameter '{name}' must be {expectedNullability} for the selected route template."));
                    continue;
                }

                if (routeValue.Constraint is { } constraint &&
                    !RouteConstraintMatchesParameter(constraint, parameter.Type))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidRouteValue,
                        parameter.Locations.FirstOrDefault(static candidate => candidate.IsInSource) ?? location,
                        $"Route value parameter '{name}' does not match route constraint '{constraint}'."));
                }
            }
        }
    }

    private static void AnalyzeBuiltInFilters(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        HandlerKind routeKind,
        Location? location)
    {
        TelegramHandlerMetadataRouteKind metadataRouteKind = ToMetadataRouteKind(routeKind);
        AttributeData[] filterAttributes = TelegramBuiltInFilterFacts.GetAttributes(method.ContainingType)
            .Concat(TelegramBuiltInFilterFacts.GetAttributes(method))
            .ToArray();

        foreach (AttributeData attribute in filterAttributes)
        {
            if (!TelegramBuiltInFilterFacts.TryGetSpec(attribute, out TelegramBuiltInFilterSpec spec))
            {
                continue;
            }

            if (!TelegramBuiltInFilterFacts.SupportsRouteKind(spec.Target, metadataRouteKind))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    TelegramBuiltInFilterFacts.GetInvalidFilterMessage(spec.Target, metadataRouteKind)));
                return;
            }
        }

        AnalyzeChatTypeFilters(
            context,
            method,
            TelegramHandlerSymbols.ChatTypeAttribute,
            "ChatTypeAttribute",
            location);
        AnalyzeChatTypeFilters(
            context,
            method,
            TelegramHandlerSymbols.SenderChatTypeAttribute,
            "SenderChatTypeAttribute",
            location);

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(method.ContainingType, TelegramHandlerSymbols.ChatIdAttribute, inherit: true)
                     .Concat(TelegramHandlerSymbols.GetAttributes(method, TelegramHandlerSymbols.ChatIdAttribute, inherit: true)))
        {
            if (attribute.ConstructorArguments.Length == 0 ||
                attribute.ConstructorArguments[0].Values.IsDefaultOrEmpty ||
                attribute.ConstructorArguments[0].Values.Any(static value => value.Value is not long chatId || chatId == 0))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    "ChatIdAttribute must specify at least one non-zero Telegram chat id."));
            }
        }

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(method.ContainingType, TelegramHandlerSymbols.ChatUsernameAttribute, inherit: true)
                     .Concat(TelegramHandlerSymbols.GetAttributes(method, TelegramHandlerSymbols.ChatUsernameAttribute, inherit: true)))
        {
            if (attribute.ConstructorArguments.Length == 0 ||
                attribute.ConstructorArguments[0].Values.IsDefaultOrEmpty ||
                attribute.ConstructorArguments[0].Values.Any(static value => value.Value is not string username || CanonicalizeChatUsername(username).Length == 0))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    "ChatUsernameAttribute must specify at least one non-empty Telegram chat username."));
            }
        }

        AnalyzeSenderIdFilters(
            context,
            method,
            TelegramHandlerSymbols.FromUserAttribute,
            "FromUserAttribute",
            "user",
            location);
        AnalyzeSenderIdFilters(
            context,
            method,
            TelegramHandlerSymbols.FromBotAttribute,
            "FromBotAttribute",
            "bot",
            location);

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(method.ContainingType, TelegramHandlerSymbols.CallbackDataPrefixAttribute, inherit: true)
                     .Concat(TelegramHandlerSymbols.GetAttributes(method, TelegramHandlerSymbols.CallbackDataPrefixAttribute, inherit: true)))
        {
            if (attribute.ConstructorArguments.Length == 0 ||
                attribute.ConstructorArguments[0].Value is not string prefix ||
                string.IsNullOrWhiteSpace(prefix))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    "CallbackDataPrefixAttribute must specify a non-empty callback data prefix."));
            }
        }

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(method.ContainingType, TelegramHandlerSymbols.MessageThreadIdAttribute, inherit: true)
                     .Concat(TelegramHandlerSymbols.GetAttributes(method, TelegramHandlerSymbols.MessageThreadIdAttribute, inherit: true)))
        {
            if (attribute.ConstructorArguments.Length == 0 ||
                attribute.ConstructorArguments[0].Values.IsDefaultOrEmpty ||
                attribute.ConstructorArguments[0].Values.Any(static value => value.Value is not long threadId || threadId <= 0))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    "MessageThreadIdAttribute must specify at least one positive Telegram message thread id."));
            }
        }
    }

    private static void AnalyzeChatTypeFilters(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        string attributeMetadataName,
        string attributeDisplayName,
        Location? location)
    {
        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(
                     method.ContainingType,
                     attributeMetadataName,
                     inherit: true)
                 .Concat(TelegramHandlerSymbols.GetAttributes(
                     method,
                     attributeMetadataName,
                     inherit: true)))
        {
            if (attribute.ConstructorArguments.Length == 0 ||
                attribute.ConstructorArguments[0].Values.IsDefaultOrEmpty ||
                attribute.ConstructorArguments[0].Values.Any(
                    static value => value.Value is not int chatType ||
                                    !TelegramChatTypeFacts.IsKnown(chatType)))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    $"{attributeDisplayName} must specify at least one known Telegram chat type."));
            }
        }
    }

    private static void AnalyzeSenderIdFilters(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        string attributeMetadataName,
        string attributeDisplayName,
        string senderDisplayName,
        Location? location)
    {
        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(
                     method.ContainingType,
                     attributeMetadataName,
                     inherit: true)
                 .Concat(TelegramHandlerSymbols.GetAttributes(
                     method,
                     attributeMetadataName,
                     inherit: true)))
        {
            if (attribute.ConstructorArguments.Length != 1 ||
                attribute.ConstructorArguments[0].Values.IsDefault ||
                attribute.ConstructorArguments[0].Values.Any(
                    static value => value.Value is not long id || id <= 0))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    $"{attributeDisplayName} must contain only positive Telegram {senderDisplayName} ids."));
            }
        }
    }

    private static void AnalyzeCustomFilters(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        HandlerKind routeKind,
        Location? location)
    {
        foreach (AttributeData attribute in GetCustomFilterAttributes(method.ContainingType)
                     .Concat(GetCustomFilterAttributes(method)))
        {
            if (attribute.AttributeClass is not INamedTypeSymbol { TypeArguments.Length: 1 } attributeType)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    "UseFilterAttribute<TFilter> must specify a filter type."));
                continue;
            }

            ITypeSymbol filterType = attributeType.TypeArguments[0];

            if (IsInvalidCustomFilterType(filterType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    "Custom Telegram filter type must be a concrete closed type."));
                continue;
            }

            if (!TryGetTelegramFilterContextTypes(filterType, out IReadOnlyList<string> contextTypes))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    "Custom Telegram filter type must implement ITelegramFilter<TContext>."));
                continue;
            }

            if (!CustomFilterSupportsRouteKind(contextTypes, routeKind))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    "Custom Telegram filter context type is not compatible with the handler route kind."));
            }
        }

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetTelegramFilterAttributes(method.ContainingType, inherit: true)
                     .Concat(TelegramHandlerSymbols.GetTelegramFilterAttributes(method, inherit: true)))
        {
            if (attribute.AttributeClass is not { } attributeType ||
                !TelegramHandlerSymbols.TryGetTelegramFilterAttributeFilterType(attribute, out ITypeSymbol filterType))
            {
                continue;
            }

            if (attributeType.IsGenericType)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    "Parameterized custom Telegram filter attribute types must be non-generic."));
                continue;
            }

            if (IsInvalidCustomFilterType(filterType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    "Custom Telegram filter type must be a concrete closed type."));
                continue;
            }

            if (!TryGetParameterizedTelegramFilterContextTypes(filterType, attributeType, out IReadOnlyList<string> typedContextTypes))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    "Parameterized custom Telegram filter attributes require a filter type that implements ITelegramFilter<TContext, TAttribute>."));
                continue;
            }

            if (!CustomFilterSupportsRouteKind(typedContextTypes, routeKind))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    "Custom Telegram filter context type is not compatible with the handler route kind."));
            }
        }
    }

    private static TelegramHandlerMetadataRouteKind ToMetadataRouteKind(HandlerKind routeKind)
    {
        return routeKind switch
        {
            HandlerKind.Command => TelegramHandlerMetadataRouteKind.Command,
            HandlerKind.Message => TelegramHandlerMetadataRouteKind.Message,
            HandlerKind.Callback => TelegramHandlerMetadataRouteKind.Callback,
            HandlerKind.ChatMember => TelegramHandlerMetadataRouteKind.ChatMember,
            _ => TelegramHandlerMetadataRouteKind.Message
        };
    }

    private static string CanonicalizeChatUsername(string? username)
    {
        if (username is null)
        {
            return string.Empty;
        }

        string value = username.Trim();
        return value.StartsWith("@", StringComparison.Ordinal) ? value.Substring(1) : value;
    }

    private static IReadOnlyList<AttributeData> GetCustomFilterAttributes(ISymbol symbol)
    {
        return TelegramHandlerSymbols.GetGenericAttributes(
            symbol,
            TelegramHandlerSymbols.GenericUseFilterAttribute,
            inherit: true);
    }

    private static bool IsInvalidCustomFilterType(ITypeSymbol type)
    {
        if (type.TypeKind is TypeKind.Interface or TypeKind.TypeParameter)
        {
            return true;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return true;
        }

        return namedType.IsAbstract ||
               namedType.IsUnboundGenericType ||
               namedType.TypeArguments.Any(static argument => argument.TypeKind == TypeKind.TypeParameter);
    }

    private static bool TryGetTelegramFilterContextTypes(
        ITypeSymbol filterType,
        out IReadOnlyList<string> contextTypes)
    {
        string[] values = filterType.AllInterfaces
            .Where(static candidate =>
                string.Equals(
                    candidate.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    TelegramHandlerSymbols.GenericTelegramFilter,
                    StringComparison.Ordinal))
            .Select(static candidate => candidate.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat))
            .ToArray();

        contextTypes = values;
        return values.Length > 0;
    }

    private static bool TryGetParameterizedTelegramFilterContextTypes(
        ITypeSymbol filterType,
        ITypeSymbol attributeType,
        out IReadOnlyList<string> contextTypes)
    {
        string[] values = filterType.AllInterfaces
            .Where(candidate =>
                string.Equals(
                    candidate.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    TelegramHandlerSymbols.GenericParameterizedTelegramFilter,
                    StringComparison.Ordinal) &&
                candidate.TypeArguments.Length == 2 &&
                SymbolEqualityComparer.Default.Equals(candidate.TypeArguments[1], attributeType))
            .Select(static candidate => candidate.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat))
            .ToArray();

        contextTypes = values;
        return values.Length > 0;
    }

    private static bool CustomFilterSupportsRouteKind(
        IReadOnlyList<string> contextTypes,
        HandlerKind routeKind)
    {
        if (contextTypes.Contains(TelegramHandlerSymbols.TelegramUpdateContext, StringComparer.Ordinal))
        {
            return true;
        }

        string expectedContext = routeKind switch
        {
            HandlerKind.Callback => TelegramHandlerSymbols.CallbackQueryContext,
            HandlerKind.ChatMember => TelegramHandlerSymbols.ChatMemberUpdatedContext,
            _ => TelegramHandlerSymbols.MessageContext
        };

        return contextTypes.Contains(expectedContext, StringComparer.Ordinal);
    }

    private static void AnalyzeChatMemberTransitions(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        HandlerKind routeKind,
        Location? location)
    {
        AttributeData[] transitionAttributes = TelegramHandlerSymbols.GetAttributes(method.ContainingType, TelegramHandlerSymbols.ChatMemberTransitionAttribute, inherit: true)
            .Concat(TelegramHandlerSymbols.GetAttributes(method, TelegramHandlerSymbols.ChatMemberTransitionAttribute, inherit: true))
            .ToArray();
        AttributeData[] changedAttributes = TelegramHandlerSymbols.GetAttributes(method.ContainingType, TelegramHandlerSymbols.ChatMemberChangedAttribute, inherit: true)
            .Concat(TelegramHandlerSymbols.GetAttributes(method, TelegramHandlerSymbols.ChatMemberChangedAttribute, inherit: true))
            .ToArray();

        if (transitionAttributes.Length == 0 && changedAttributes.Length == 0)
        {
            return;
        }

        if (routeKind != HandlerKind.ChatMember)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidFilter,
                location,
                "Chat member transition attributes can only be used on chat member update handlers."));
            return;
        }

        foreach (AttributeData attribute in transitionAttributes)
        {
            if (attribute.ConstructorArguments.Length == 0 ||
                attribute.ConstructorArguments[0].Value is not int transition ||
                !TelegramMemberStatusFacts.TryMapTransition(transition, out _, out _))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    "ChatMemberTransitionAttribute must specify a known Telegram member transition."));
            }
        }

        foreach (AttributeData attribute in changedAttributes)
        {
            if (attribute.ConstructorArguments.Length < 2 ||
                attribute.ConstructorArguments[0].Value is not int oldStatus ||
                attribute.ConstructorArguments[1].Value is not int newStatus ||
                !TelegramMemberStatusFacts.IsValid(oldStatus) ||
                !TelegramMemberStatusFacts.IsValid(newStatus))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    "ChatMemberChangedAttribute must specify non-empty known Telegram member status sets."));
            }
        }
    }

    private static void AnalyzeTelegramRoleRequirements(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        Location? location)
    {
        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(method.ContainingType, TelegramHandlerSymbols.RequireTelegramRoleAttribute, inherit: true)
                     .Concat(TelegramHandlerSymbols.GetAttributes(method, TelegramHandlerSymbols.RequireTelegramRoleAttribute, inherit: true)))
        {
            if (!TelegramMemberStatusFacts.TryGetRoleRequirementMask(attribute, out int allowedStatuses) ||
                !TelegramMemberStatusFacts.IsValid(allowedStatuses))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFilter,
                    location,
                    "RequireTelegramRoleAttribute must specify non-empty known Telegram member status sets."));
            }
        }
    }

    private static bool TryParseRouteTemplate(
        string? template,
        out IReadOnlyDictionary<string, RouteValueDefinition> values,
        out string reason)
    {
        values = new Dictionary<string, RouteValueDefinition>(StringComparer.Ordinal);
        reason = string.Empty;

        if (template is null ||
            string.IsNullOrWhiteSpace(template))
        {
            reason = "Route template must not be empty.";
            return false;
        }

        Regex placeholderRegex = new Regex(
            @"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:(?<nameOptional>\?)|:(?<constraint>[A-Za-z][A-Za-z0-9_]*)(?<constraintOptional>\?)?)?\}",
            RegexOptions.CultureInvariant);
        Dictionary<string, RouteValueDefinition> parsedValues = new Dictionary<string, RouteValueDefinition>(StringComparer.Ordinal);
        int position = 0;

        foreach (Match match in placeholderRegex.Matches(template))
        {
            if (match.Index != position &&
                ContainsBrace(template.Substring(position, match.Index - position)))
            {
                reason = $"Route template '{template}' contains an invalid placeholder.";
                return false;
            }

            string name = match.Groups["name"].Value;
            bool hasNameOptional = match.Groups["nameOptional"].Success;
            bool hasConstraintOptional = match.Groups["constraintOptional"].Success;
            string constraint = match.Groups["constraint"].Success
                ? match.Groups["constraint"].Value
                : "string";

            if (parsedValues.ContainsKey(name))
            {
                reason = $"Route template '{template}' contains duplicate placeholder '{name}'.";
                return false;
            }

            parsedValues.Add(name, new RouteValueDefinition(constraint, hasNameOptional || hasConstraintOptional));

            if (constraint is not ("string" or "int" or "long"))
            {
                reason = $"Route template '{template}' uses unsupported placeholder constraint '{constraint}'.";
                return false;
            }

            position = match.Index + match.Length;
        }

        if (ContainsBrace(template.Substring(position)))
        {
            reason = $"Route template '{template}' contains an invalid placeholder.";
            return false;
        }

        values = parsedValues;
        return true;
    }

    private static bool TryParseRouteRegex(
        string? pattern,
        out IReadOnlyDictionary<string, RouteValueDefinition> values,
        out string reason)
    {
        values = new Dictionary<string, RouteValueDefinition>(StringComparer.Ordinal);
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(pattern))
        {
            reason = "Route regex pattern must not be empty.";
            return false;
        }

        Regex regex;

        try
        {
            regex = new Regex(pattern, RegexOptions.CultureInvariant);
        }
        catch (ArgumentException exception)
        {
            reason = $"Route regex pattern is invalid: {exception.Message}";
            return false;
        }

        values = regex
            .GetGroupNames()
            .Where(static name => !int.TryParse(name, out _))
            .ToDictionary(
                static name => name,
                static _ => new RouteValueDefinition(Constraint: null, IsOptional: false),
                StringComparer.Ordinal);
        return true;
    }

    private static string? GetConstructorString(AttributeData attribute)
    {
        return attribute.ConstructorArguments.Length > 0 &&
               attribute.ConstructorArguments[0].Value is string value
            ? value.Trim()
            : null;
    }

    private static bool TryGetPrefixes(
        AttributeData attribute,
        out IReadOnlyList<string> prefixes,
        out string reason)
    {
        prefixes = ["/"];
        reason = string.Empty;

        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (!string.Equals(argument.Key, "Prefixes", StringComparison.Ordinal))
            {
                continue;
            }

            if (argument.Value.Values.IsDefaultOrEmpty)
            {
                reason = "Command route prefixes must contain at least one non-empty prefix.";
                return false;
            }

            List<string> values = new List<string>();

            foreach (TypedConstant value in argument.Value.Values)
            {
                if (value.Value is not string prefix ||
                    string.IsNullOrWhiteSpace(prefix))
                {
                    reason = "Command route prefixes must contain only non-empty prefix values.";
                    return false;
                }

                values.Add(prefix.Trim());
            }

            prefixes = values.Distinct(StringComparer.Ordinal).ToArray();
            return true;
        }

        return true;
    }

    private static int GetCommandPrefixMode(AttributeData attribute)
    {
        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, "PrefixMode", StringComparison.Ordinal) &&
                argument.Value.Value is int value)
            {
                return value;
            }
        }

        return CommandPrefixModeRequired;
    }

    private static bool TryValidateCommandPrefixMode(
        AttributeData attribute,
        int prefixMode,
        out string reason)
    {
        reason = string.Empty;

        if (!IsSupportedCommandPrefixMode(prefixMode))
        {
            reason = "Command route prefix mode must be Required, Optional, or NoPrefix.";
            return false;
        }

        if (prefixMode == CommandPrefixModeNoPrefix && HasNamedArgument(attribute, "Prefixes"))
        {
            reason = "Command route prefixes must not be configured when PrefixMode is NoPrefix.";
            return false;
        }

        return true;
    }

    private static bool IsSupportedCommandPrefixMode(int value)
    {
        return value is CommandPrefixModeRequired or
            CommandPrefixModeOptional or
            CommandPrefixModeNoPrefix;
    }

    private static bool HasNamedArgument(
        AttributeData attribute,
        string name)
    {
        return attribute.NamedArguments.Any(argument => string.Equals(argument.Key, name, StringComparison.Ordinal));
    }

    private static bool CommandPatternStartsWithPrefix(
        string pattern,
        IReadOnlyList<string> prefixes)
    {
        string normalized = pattern.Trim();

        return prefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool GetNamedBool(
        AttributeData attribute,
        string name,
        bool defaultValue)
    {
        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, name, StringComparison.Ordinal) &&
                argument.Value.Value is bool value)
            {
                return value;
            }
        }

        return defaultValue;
    }

    private static bool ContainsBrace(string value)
    {
        return value.IndexOf('{') >= 0 ||
               value.IndexOf('}') >= 0;
    }

    private static bool IsSupportedRouteValueParameterType(ITypeSymbol type)
    {
        if (type.SpecialType is SpecialType.System_String or
            SpecialType.System_Int32 or
            SpecialType.System_Int64)
        {
            return true;
        }

        return type is INamedTypeSymbol
        {
            OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
            TypeArguments.Length: 1
        } nullable &&
        nullable.TypeArguments[0].SpecialType is
            SpecialType.System_Int32 or
            SpecialType.System_Int64;
    }

    private static bool IsNullableRouteValueParameter(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return type.NullableAnnotation == NullableAnnotation.Annotated;
        }

        return type is INamedTypeSymbol
        {
            OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
        };
    }

    private static SpecialType GetRouteValueParameterSpecialType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments.Length: 1
            } nullable)
        {
            return nullable.TypeArguments[0].SpecialType;
        }

        return type.SpecialType;
    }

    private static bool RouteConstraintMatchesParameter(
        string constraint,
        ITypeSymbol parameterType)
    {
        SpecialType specialType = GetRouteValueParameterSpecialType(parameterType);

        return constraint switch
        {
            "string" => specialType == SpecialType.System_String,
            "int" => specialType == SpecialType.System_Int32,
            "long" => specialType == SpecialType.System_Int64,
            _ => false
        };
    }

    private readonly record struct RouteValueDefinition(
        string? Constraint,
        bool IsOptional);

    private static void ValidateCallbackRoute(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        ITypeSymbol? callbackPayloadType,
        bool hasRawCallback,
        Location? location)
    {
        if (callbackPayloadType is null)
        {
            if (hasRawCallback)
            {
                foreach (IParameterSymbol parameter in method.Parameters.Where(static parameter => HasCallbackDataAttribute(parameter.Type)))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidTypedCallback,
                        parameter.Locations.FirstOrDefault(static candidate => candidate.IsInSource) ?? location,
                        $"Raw CallbackAttribute handler '{method.Name}' cannot bind typed callback payload parameter '{parameter.Name}'. Use CallbackAttribute<TPayload>."));
                }
            }

            return;
        }

        if (IsInvalidCallbackPayloadType(callbackPayloadType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidTypedCallback,
                location,
                $"CallbackAttribute<TPayload> on '{method.Name}' must use a concrete closed payload type."));
        }

        IParameterSymbol[] payloadParameters = method.Parameters
            .Where(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, callbackPayloadType))
            .ToArray();

        if (payloadParameters.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidTypedCallback,
                location,
                $"CallbackAttribute<{callbackPayloadType.Name}> handler '{method.Name}' must declare exactly one {callbackPayloadType.Name} payload parameter."));
        }
    }

    private static bool HasCallbackDataAttribute(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               TelegramHandlerSymbols.HasAttribute(namedType, TelegramHandlerSymbols.CallbackDataAttribute);
    }

    private static bool IsInvalidCallbackPayloadType(ITypeSymbol type)
    {
        if (type.TypeKind is TypeKind.Interface or TypeKind.TypeParameter)
        {
            return true;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return true;
        }

        return namedType.IsAbstract ||
               namedType.IsUnboundGenericType ||
               namedType.TypeArguments.Any(static argument => argument.TypeKind == TypeKind.TypeParameter);
    }

    private static void ValidateContextParameter(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        HandlerKind kind,
        string displayName,
        Location? location)
    {
        string expectedContextType = kind switch
        {
            HandlerKind.Callback => TelegramHandlerSymbols.CallbackQueryContext,
            HandlerKind.ChatMember => TelegramHandlerSymbols.ChatMemberUpdatedContext,
            _ => TelegramHandlerSymbols.MessageContext
        };
        string expectedContextName = kind switch
        {
            HandlerKind.Callback => "CallbackQueryContext",
            HandlerKind.ChatMember => "ChatMemberUpdatedContext",
            _ => "MessageContext"
        };
        IParameterSymbol[] contextParameters = method.Parameters
            .Where(static parameter =>
                TelegramHandlerSymbols.IsType(parameter.Type, TelegramHandlerSymbols.MessageContext) ||
                TelegramHandlerSymbols.IsType(parameter.Type, TelegramHandlerSymbols.CallbackQueryContext) ||
                TelegramHandlerSymbols.IsType(parameter.Type, TelegramHandlerSymbols.ChatMemberUpdatedContext) ||
                TelegramHandlerSymbols.IsType(parameter.Type, TelegramHandlerSymbols.TelegramUpdateContext))
            .ToArray();

        if (contextParameters.Length != 1 ||
            !TelegramHandlerSymbols.IsType(contextParameters[0].Type, expectedContextType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidContextParameter,
                location,
                GetDiagnosticHandlerKind(kind),
                displayName,
                expectedContextName));
        }
    }

    private static string GetDiagnosticHandlerKind(HandlerKind kind)
    {
        return kind switch
        {
            HandlerKind.Callback => "callback",
            HandlerKind.ChatMember => "chatmember",
            _ => "message"
        };
    }

    private static void ValidateCancellationTokens(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        string displayName,
        Location? location)
    {
        if (method.Parameters.Count(static parameter =>
                TelegramHandlerSymbols.IsType(parameter.Type, TelegramHandlerSymbols.CancellationToken)) <= 1)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MultipleCancellationTokens,
            location,
            displayName));
    }

    private static bool IsSupportedReturnType(ITypeSymbol returnType)
    {
        return TelegramHandlerSymbols.IsType(returnType, TelegramHandlerSymbols.Task) ||
               TelegramHandlerSymbols.IsType(returnType, TelegramHandlerSymbols.ValueTask);
    }

    private static bool IsValidCommand(string? command)
    {
        return command is not null &&
               !string.IsNullOrWhiteSpace(command) &&
               !command.StartsWith("/", StringComparison.Ordinal) &&
               !command.Contains("@", StringComparison.Ordinal) &&
               !command.Any(char.IsWhiteSpace);
    }

    private static bool HasAnyRouteAttribute(
        IMethodSymbol method,
        bool includeClassRouteAttributes)
    {
        return TelegramHandlerSymbols.HasAnyRouteAttribute(method) ||
               (includeClassRouteAttributes &&
                TelegramHandlerSymbols.HasAnyRouteAttribute(method.ContainingType));
    }

    private static IReadOnlyList<AttributeData> GetRouteAttributes(
        IMethodSymbol method,
        string metadataName,
        bool includeClassRouteAttributes)
    {
        IReadOnlyList<AttributeData> methodAttributes = TelegramHandlerSymbols.GetAttributes(method, metadataName, inherit: true);

        if (!includeClassRouteAttributes)
        {
            return methodAttributes;
        }

        IReadOnlyList<AttributeData> typeAttributes = TelegramHandlerSymbols.GetAttributes(method.ContainingType, metadataName, inherit: true);

        return typeAttributes.Count == 0
            ? methodAttributes
            : typeAttributes.Concat(methodAttributes).ToArray();
    }

    private static IReadOnlyList<AttributeData> GetGenericRouteAttributes(
        IMethodSymbol method,
        string metadataName,
        bool includeClassRouteAttributes)
    {
        IReadOnlyList<AttributeData> methodAttributes = TelegramHandlerSymbols.GetGenericAttributes(method, metadataName, inherit: true);

        if (!includeClassRouteAttributes)
        {
            return methodAttributes;
        }

        IReadOnlyList<AttributeData> typeAttributes = TelegramHandlerSymbols.GetGenericAttributes(method.ContainingType, metadataName, inherit: true);

        return typeAttributes.Count == 0
            ? methodAttributes
            : typeAttributes.Concat(methodAttributes).ToArray();
    }
}
