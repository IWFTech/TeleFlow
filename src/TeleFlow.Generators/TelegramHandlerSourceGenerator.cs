using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace TeleFlow.Generators;

[Generator]
public sealed partial class TelegramHandlerSourceGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat FullyQualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<ImmutableArray<GeneratedHandlerMethod?>> methodHandlers = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is MethodDeclarationSyntax,
                static (syntaxContext, _) => GetHandlerMethod(syntaxContext))
            .Where(static method => method is not null)
            .Collect();

        IncrementalValueProvider<ImmutableArray<GeneratedHandlerMethod?>> classBasedHandlers = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax,
                static (syntaxContext, _) => GetClassBasedHandlerMethod(syntaxContext))
            .Where(static method => method is not null)
            .Collect();

        IncrementalValueProvider<ImmutableArray<GeneratedErrorHandlerMethod?>> errorHandlers = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is MethodDeclarationSyntax,
                static (syntaxContext, _) => GetErrorHandlerMethod(syntaxContext))
            .Where(static method => method is not null)
            .Collect();

        IncrementalValueProvider<ImmutableArray<GeneratedStateGroup?>> stateGroups = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
                static (syntaxContext, _) => GetStateGroup(syntaxContext))
            .Where(static group => group is not null)
            .Collect();

        context.RegisterSourceOutput(
            methodHandlers.Combine(classBasedHandlers).Combine(errorHandlers),
            static (sourceContext, collectedHandlers) =>
        {
            GeneratedHandlerMethod[] handlers = collectedHandlers.Left.Left
                .Concat(collectedHandlers.Left.Right)
                .Where(static handler => handler is not null)
                .Cast<GeneratedHandlerMethod>()
                .GroupBy(static handler => handler.SignatureKey, StringComparer.Ordinal)
                .Select(static group => group.First())
                .OrderBy(static handler => handler.HandlerTypeMetadataName, StringComparer.Ordinal)
                .ThenBy(static handler => handler.SourcePath, StringComparer.Ordinal)
                .ThenBy(static handler => handler.SourceSpanStart)
                .ToArray();
            GeneratedErrorHandlerMethod[] errors = collectedHandlers.Right
                .Where(static handler => handler is not null)
                .Cast<GeneratedErrorHandlerMethod>()
                .GroupBy(static handler => handler.SignatureKey, StringComparer.Ordinal)
                .Select(static group => group.First())
                .OrderBy(static handler => handler.HandlerTypeMetadataName, StringComparer.Ordinal)
                .ThenBy(static handler => handler.SourcePath, StringComparer.Ordinal)
                .ThenBy(static handler => handler.SourceSpanStart)
                .ToArray();

            if (handlers.Length == 0 && errors.Length == 0)
            {
                return;
            }

            sourceContext.AddSource(
                "TeleFlow.Telegram.GeneratedHandlers.g.cs",
                SourceText.From(GenerateSource(handlers, errors), Encoding.UTF8));
        });

        context.RegisterSourceOutput(stateGroups, static (sourceContext, collectedGroups) =>
        {
            GeneratedStateGroup[] groups = collectedGroups
                .Where(static group => group is not null)
                .Cast<GeneratedStateGroup>()
                .GroupBy(static group => group.TypeMetadataName, StringComparer.Ordinal)
                .Select(static group => group.First())
                .OrderBy(static group => group.TypeMetadataName, StringComparer.Ordinal)
                .ToArray();

            if (groups.Length == 0)
            {
                return;
            }

            sourceContext.AddSource(
                "TeleFlow.StateGroups.g.cs",
                SourceText.From(GenerateStateGroupsSource(groups), Encoding.UTF8));
        });
    }

    private static GeneratedHandlerMethod? GetHandlerMethod(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not IMethodSymbol method ||
            IsClassBasedHandlerType(method.ContainingType) ||
            !TelegramHandlerSymbols.HasAnyRouteAttribute(method))
        {
            return null;
        }

        if (!TryBuildHandler(method, includeClassRouteAttributes: false, out GeneratedHandlerMethod handler))
        {
            return null;
        }

        return handler;
    }

    private static GeneratedHandlerMethod? GetClassBasedHandlerMethod(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol type ||
            !IsClassBasedHandlerType(type))
        {
            return null;
        }

        ImmutableArray<IMethodSymbol> handleMethods = GetDeclaredHandleAsyncMethods(type);

        if (handleMethods.Length != 1 ||
            (!TelegramHandlerSymbols.HasAnyRouteAttribute(type) &&
             !TelegramHandlerSymbols.HasAnyRouteAttribute(handleMethods[0])))
        {
            return null;
        }

        if (!TryBuildHandler(handleMethods[0], includeClassRouteAttributes: true, out GeneratedHandlerMethod handler))
        {
            return null;
        }

        return handler;
    }

    private static GeneratedErrorHandlerMethod? GetErrorHandlerMethod(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not IMethodSymbol method ||
            !TelegramHandlerSymbols.HasAnyErrorAttribute(method))
        {
            return null;
        }

        if (!TryBuildErrorHandler(method, out GeneratedErrorHandlerMethod handler))
        {
            return null;
        }

        return handler;
    }

    private static bool TryBuildHandler(
        IMethodSymbol method,
        bool includeClassRouteAttributes,
        out GeneratedHandlerMethod handler)
    {
        handler = null!;

        ImmutableArray<GeneratedRoute> routes = BuildRoutes(method, includeClassRouteAttributes, out ITypeSymbol? callbackPayloadType);

        if (routes.Length == 0 ||
            method.DeclaredAccessibility != Accessibility.Public ||
            method.IsStatic ||
            method.IsGenericMethod ||
            method.ContainingType.TypeKind is TypeKind.Interface ||
            method.ContainingType.IsAbstract ||
            !IsClassBasedRouteCompatible(method.ContainingType, routes[0].Kind, callbackPayloadType) ||
            !IsSupportedReturnType(method.ReturnType) ||
            !TryGetExpectedContextType(routes[0].Kind, out string expectedContextType) ||
            !HasExactlyOneExpectedContext(method, expectedContextType) ||
            !HasValidCallbackPayloadParameter(method, callbackPayloadType) ||
            !TryResolveRouteValueNames(method, routes, out ImmutableHashSet<string> routeValueNames))
        {
            return false;
        }

        if (!TryBuildStates(method, out ImmutableArray<string> states, out string? sceneName))
        {
            return false;
        }

        MethodDeclarationSyntax? syntax = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
        Location? location = method.Locations.FirstOrDefault(location => location.IsInSource);
        ImmutableArray<GeneratedParameter> parameters = BuildParameters(method, routes[0].Kind, expectedContextType, callbackPayloadType, routeValueNames);
        bool hasCallbackRoute = routes.Any(static route => route.Kind == GeneratedHandlerKind.Callback);

        if (parameters.Length == 0)
        {
            return false;
        }

        if (!TryBuildAutoAnswerCallback(method, hasCallbackRoute, out GeneratedAutoAnswerCallback? autoAnswerCallback))
        {
            return false;
        }

        handler = new GeneratedHandlerMethod(
            SignatureKey: method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            HandlerTypeName: method.ContainingType.ToDisplayString(FullyQualifiedFormat),
            HandlerTypeMetadataName: method.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            MethodName: method.Name,
            ModuleName: GetModuleName(method.ContainingType),
            SceneName: sceneName,
            CallbackPayloadType: callbackPayloadType?.ToDisplayString(FullyQualifiedFormat),
            AutoAnswerCallback: autoAnswerCallback,
            Routes: routes,
            States: states,
            Parameters: parameters,
            SourcePath: location?.SourceTree?.FilePath ?? string.Empty,
            SourceSpanStart: location?.SourceSpan.Start ?? syntax?.SpanStart ?? 0);

        return true;
    }

    private static bool TryBuildErrorHandler(
        IMethodSymbol method,
        out GeneratedErrorHandlerMethod handler)
    {
        handler = null!;

        IReadOnlyList<AttributeData> errorAttributes = TelegramHandlerSymbols.GetErrorAttributes(method, inherit: true);

        if (errorAttributes.Count == 0 ||
            method.DeclaredAccessibility != Accessibility.Public ||
            method.IsStatic ||
            method.IsGenericMethod ||
            method.ContainingType.TypeKind is TypeKind.Interface ||
            method.ContainingType.IsAbstract ||
            !TryGetErrorReturnKind(method.ReturnType, out GeneratedErrorReturnKind returnKind) ||
            !TryBuildErrorExceptionTypes(errorAttributes, out ImmutableArray<ITypeSymbol?> exceptionTypes, out ImmutableArray<string?> exceptionTypeNames) ||
            !TryBuildErrorParameters(method, exceptionTypes, out ImmutableArray<GeneratedErrorParameter> parameters, out string? telegramContextType))
        {
            return false;
        }

        MethodDeclarationSyntax? syntax = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
        Location? location = method.Locations.FirstOrDefault(static location => location.IsInSource);

        handler = new GeneratedErrorHandlerMethod(
            SignatureKey: method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            HandlerTypeName: method.ContainingType.ToDisplayString(FullyQualifiedFormat),
            HandlerTypeMetadataName: method.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            MethodName: method.Name,
            ModuleName: GetModuleName(method.ContainingType),
            ExceptionTypes: exceptionTypeNames,
            TelegramContextType: telegramContextType,
            ReturnKind: returnKind,
            Parameters: parameters,
            SourcePath: location?.SourceTree?.FilePath ?? string.Empty,
            SourceSpanStart: location?.SourceSpan.Start ?? syntax?.SpanStart ?? 0);

        return true;
    }

    private static bool TryBuildErrorExceptionTypes(
        IReadOnlyList<AttributeData> attributes,
        out ImmutableArray<ITypeSymbol?> exceptionTypes,
        out ImmutableArray<string?> exceptionTypeNames)
    {
        ImmutableArray<ITypeSymbol?>.Builder symbols = ImmutableArray.CreateBuilder<ITypeSymbol?>();
        ImmutableArray<string?>.Builder names = ImmutableArray.CreateBuilder<string?>();

        foreach (AttributeData attribute in attributes)
        {
            if (TelegramHandlerSymbols.IsGenericAttribute(attribute, TelegramHandlerSymbols.GenericErrorAttribute))
            {
                if (attribute.AttributeClass is not { TypeArguments.Length: 1 } genericAttribute ||
                    !IsExceptionType(genericAttribute.TypeArguments[0]))
                {
                    exceptionTypes = [];
                    exceptionTypeNames = [];
                    return false;
                }

                symbols.Add(genericAttribute.TypeArguments[0]);
                names.Add(genericAttribute.TypeArguments[0].ToDisplayString(FullyQualifiedFormat));
                continue;
            }

            symbols.Add(null);
            names.Add(null);
        }

        exceptionTypes = symbols.ToImmutable();
        exceptionTypeNames = names.ToImmutable();
        return exceptionTypes.Length > 0;
    }

    private static bool TryBuildErrorParameters(
        IMethodSymbol method,
        ImmutableArray<ITypeSymbol?> exceptionTypes,
        out ImmutableArray<GeneratedErrorParameter> parameters,
        out string? telegramContextType)
    {
        parameters = [];
        telegramContextType = null;

        if (method.Parameters.Count(static parameter =>
                TelegramHandlerSymbols.IsType(parameter.Type, TelegramHandlerSymbols.TelegramErrorContext)) > 1 ||
            method.Parameters.Count(static parameter =>
                TelegramHandlerSymbols.IsType(parameter.Type, TelegramHandlerSymbols.CancellationToken)) > 1)
        {
            return false;
        }

        IParameterSymbol[] exceptionParameters = method.Parameters
            .Where(static parameter => IsExceptionType(parameter.Type))
            .ToArray();

        if (exceptionParameters.Length > 1)
        {
            return false;
        }

        IParameterSymbol[] telegramContextParameters = method.Parameters
            .Where(static parameter => IsTelegramContextType(parameter.Type))
            .ToArray();

        if (telegramContextParameters.Length > 1)
        {
            return false;
        }

        IParameterSymbol? exceptionParameter = exceptionParameters.FirstOrDefault();

        if (exceptionParameter is not null)
        {
            foreach (ITypeSymbol? exceptionType in exceptionTypes)
            {
                if (exceptionType is null)
                {
                    if (!TelegramHandlerSymbols.IsType(exceptionParameter.Type, TelegramHandlerSymbols.Exception))
                    {
                        return false;
                    }

                    continue;
                }

                if (!IsAssignableFrom(exceptionParameter.Type, exceptionType))
                {
                    return false;
                }
            }
        }

        telegramContextType = telegramContextParameters.FirstOrDefault()?.Type.ToDisplayString(FullyQualifiedFormat);
        parameters = BuildErrorParameters(method);
        return true;
    }

    private static bool TryBuildAutoAnswerCallback(
        IMethodSymbol method,
        bool hasCallbackRoute,
        out GeneratedAutoAnswerCallback? autoAnswerCallback)
    {
        autoAnswerCallback = null;

        AttributeData? methodAttribute = TelegramHandlerSymbols.GetFirstAttribute(
            method,
            TelegramHandlerSymbols.AutoAnswerCallbackAttribute,
            inherit: true);

        if (methodAttribute is not null && !hasCallbackRoute)
        {
            return false;
        }

        if (!hasCallbackRoute)
        {
            return true;
        }

        AttributeData? attribute = methodAttribute ??
            TelegramHandlerSymbols.GetFirstAttribute(
                method.ContainingType,
                TelegramHandlerSymbols.AutoAnswerCallbackAttribute,
                inherit: true);

        if (attribute is null)
        {
            return true;
        }

        string? text = GetConstructorString(attribute);
        if (text is not null && string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        autoAnswerCallback = new GeneratedAutoAnswerCallback(
            GetNamedBool(attribute, "Enabled", defaultValue: true),
            text,
            GetNamedBool(attribute, "ShowAlert", defaultValue: false));
        return true;
    }

    private static ImmutableArray<GeneratedRoute> BuildRoutes(
        IMethodSymbol method,
        bool includeClassRouteAttributes,
        out ITypeSymbol? callbackPayloadType)
    {
        callbackPayloadType = null;
        bool hasRawCallback = GetRouteAttributes(
            method,
            TelegramHandlerSymbols.CallbackAttribute,
            includeClassRouteAttributes)
            .Count > 0;
        IReadOnlyList<AttributeData> genericCallbackAttributes = GetGenericRouteAttributes(
            method,
            TelegramHandlerSymbols.GenericCallbackAttribute,
            includeClassRouteAttributes);
        AttributeData? genericCallback = genericCallbackAttributes.Count > 0
            ? genericCallbackAttributes[0]
            : null;

        if (genericCallback?.AttributeClass is { TypeArguments.Length: 1 } genericCallbackType)
        {
            callbackPayloadType = genericCallbackType.TypeArguments[0];
        }

        int messageRouteAttributeCount =
            GetRouteAttributes(method, TelegramHandlerSymbols.CommandAttribute, includeClassRouteAttributes).Count +
            GetRouteAttributes(method, TelegramHandlerSymbols.MessageAttribute, includeClassRouteAttributes).Count +
            GetRouteAttributes(method, TelegramHandlerSymbols.TextAttribute, includeClassRouteAttributes).Count +
            GetRouteAttributes(method, TelegramHandlerSymbols.TextTemplateAttribute, includeClassRouteAttributes).Count +
            GetRouteAttributes(method, TelegramHandlerSymbols.CommandTemplateAttribute, includeClassRouteAttributes).Count +
            GetRouteAttributes(method, TelegramHandlerSymbols.TextRegexAttribute, includeClassRouteAttributes).Count +
            GetRouteAttributes(method, TelegramHandlerSymbols.CommandRegexAttribute, includeClassRouteAttributes).Count;
        bool hasChatMemberUpdated = GetRouteAttributes(
            method,
            TelegramHandlerSymbols.ChatMemberUpdatedAttribute,
            includeClassRouteAttributes).Count > 0;
        bool hasMyChatMemberUpdated = GetRouteAttributes(
            method,
            TelegramHandlerSymbols.MyChatMemberUpdatedAttribute,
            includeClassRouteAttributes).Count > 0;
        bool hasChatMemberRoute = hasChatMemberUpdated || hasMyChatMemberUpdated;

        if ((hasRawCallback || callbackPayloadType is not null) &&
            (messageRouteAttributeCount > 0 || hasChatMemberRoute || (hasRawCallback && callbackPayloadType is not null)))
        {
            return [];
        }

        if (hasChatMemberRoute && messageRouteAttributeCount > 0)
        {
            return [];
        }

        if (!hasChatMemberRoute && HasChatMemberTransitionAttributes(method))
        {
            return [];
        }

        if (!TryBuildFilters(method, out ImmutableArray<GeneratedFilter> filters))
        {
            return [];
        }

        if (!TryBuildRoleRequirements(method, out ImmutableArray<int> roleRequirements))
        {
            return [];
        }

        if (hasChatMemberRoute)
        {
            if (!TryPrepareCustomFilters(filters, GeneratedHandlerKind.ChatMember, out ImmutableArray<GeneratedFilter> chatMemberFilters) ||
                chatMemberFilters.Any(static filter => filter.CustomTypeName is null &&
                                             !SupportsGeneratedFilter(filter, GeneratedHandlerKind.ChatMember)) ||
                !TryBuildChatMemberTransitions(method, out ImmutableArray<GeneratedChatMemberTransition> transitions))
            {
                return [];
            }

            filters = chatMemberFilters;
            ImmutableArray<GeneratedRoute>.Builder chatMemberRoutes = ImmutableArray.CreateBuilder<GeneratedRoute>();

            if (hasChatMemberUpdated)
            {
                chatMemberRoutes.Add(new GeneratedRoute(
                    GeneratedHandlerKind.ChatMember,
                    GeneratedRouteKind.ChatMemberUpdated,
                    Command: null,
                    Pattern: null,
                    CommandPrefixes: ["/"],
                    AllowSpaceAfterPrefix: false,
                    IgnoreCase: true,
                    TextFilters: [],
                    filters,
                    transitions,
                    roleRequirements,
                    RouteValues: ImmutableDictionary<string, GeneratedRouteValue>.Empty));
            }

            if (hasMyChatMemberUpdated)
            {
                chatMemberRoutes.Add(new GeneratedRoute(
                    GeneratedHandlerKind.ChatMember,
                    GeneratedRouteKind.MyChatMemberUpdated,
                    Command: null,
                    Pattern: null,
                    CommandPrefixes: ["/"],
                    AllowSpaceAfterPrefix: false,
                    IgnoreCase: true,
                    TextFilters: [],
                    filters,
                    transitions,
                    roleRequirements,
                    RouteValues: ImmutableDictionary<string, GeneratedRouteValue>.Empty));
            }

            return chatMemberRoutes.ToImmutable();
        }

        if (hasRawCallback || callbackPayloadType is not null)
        {
            if (!TryPrepareCustomFilters(filters, GeneratedHandlerKind.Callback, out ImmutableArray<GeneratedFilter> callbackFilters) ||
                callbackFilters.Any(static filter => filter.CustomTypeName is null &&
                                             !SupportsGeneratedFilter(filter, GeneratedHandlerKind.Callback)))
            {
                return [];
            }

            filters = callbackFilters;
            return
            [
                new GeneratedRoute(
                    GeneratedHandlerKind.Callback,
                    GeneratedRouteKind.Callback,
                    Command: null,
                    Pattern: null,
                    CommandPrefixes: ["/"],
                    AllowSpaceAfterPrefix: false,
                    IgnoreCase: true,
                    TextFilters: [],
                    filters,
                    ChatMemberTransitions: [],
                    RoleRequirements: roleRequirements,
                    RouteValues: ImmutableDictionary<string, GeneratedRouteValue>.Empty)
            ];
        }

        if (!TryPrepareCustomFilters(filters, GeneratedHandlerKind.Message, out ImmutableArray<GeneratedFilter> messageFilters) ||
            messageFilters.Any(static filter => filter.CustomTypeName is null &&
                                         !SupportsGeneratedFilter(filter, GeneratedHandlerKind.Message)))
        {
            return [];
        }

        filters = messageFilters;
        ImmutableArray<GeneratedRoute>.Builder routes = ImmutableArray.CreateBuilder<GeneratedRoute>();

        foreach (AttributeData attribute in GetRouteAttributes(
                     method,
                     TelegramHandlerSymbols.CommandAttribute,
                     includeClassRouteAttributes))
        {
            string? command = GetConstructorString(attribute);

            if (!IsValidCommand(command) ||
                !TryGetPrefixes(attribute, out ImmutableArray<string> prefixes))
            {
                return [];
            }

            routes.Add(new GeneratedRoute(
                GeneratedHandlerKind.Command,
                GeneratedRouteKind.CommandExact,
                command,
                command,
                prefixes,
                GetNamedBool(attribute, "AllowSpaceAfterPrefix", defaultValue: false),
                GetNamedBool(attribute, "IgnoreCase", defaultValue: true),
                TextFilters: [],
                filters,
                ChatMemberTransitions: [],
                RoleRequirements: roleRequirements,
                RouteValues: ImmutableDictionary<string, GeneratedRouteValue>.Empty));
        }

        foreach (AttributeData attribute in GetRouteAttributes(
                     method,
                     TelegramHandlerSymbols.CommandTemplateAttribute,
                     includeClassRouteAttributes))
        {
            string? template = GetConstructorString(attribute);

            if (template is null ||
                string.IsNullOrWhiteSpace(template) ||
                !TryParseTemplateRouteValues(template, out ImmutableDictionary<string, GeneratedRouteValue> routeValues) ||
                !TryGetPrefixes(attribute, out ImmutableArray<string> prefixes) ||
                CommandPatternStartsWithPrefix(template, prefixes))
            {
                return [];
            }

            routes.Add(new GeneratedRoute(
                GeneratedHandlerKind.Command,
                GeneratedRouteKind.CommandTemplate,
                Command: null,
                Pattern: template.Trim(),
                prefixes,
                GetNamedBool(attribute, "AllowSpaceAfterPrefix", defaultValue: false),
                GetNamedBool(attribute, "IgnoreCase", defaultValue: true),
                TextFilters: [],
                filters,
                ChatMemberTransitions: [],
                RoleRequirements: roleRequirements,
                routeValues));
        }

        foreach (AttributeData attribute in GetRouteAttributes(
                     method,
                     TelegramHandlerSymbols.CommandRegexAttribute,
                     includeClassRouteAttributes))
        {
            string? pattern = GetConstructorString(attribute);

            if (string.IsNullOrWhiteSpace(pattern) ||
                !TryParseRegexRouteValues(pattern, out ImmutableDictionary<string, GeneratedRouteValue> routeValues) ||
                !TryGetPrefixes(attribute, out ImmutableArray<string> prefixes))
            {
                return [];
            }

            routes.Add(new GeneratedRoute(
                GeneratedHandlerKind.Command,
                GeneratedRouteKind.CommandRegex,
                Command: null,
                Pattern: pattern,
                prefixes,
                GetNamedBool(attribute, "AllowSpaceAfterPrefix", defaultValue: false),
                GetNamedBool(attribute, "IgnoreCase", defaultValue: true),
                TextFilters: [],
                filters,
                ChatMemberTransitions: [],
                RoleRequirements: roleRequirements,
                routeValues));
        }

        ImmutableArray<GeneratedTextFilter> textFilters = BuildTextFilters(method, includeClassRouteAttributes);
        bool hasMessageAttribute = GetRouteAttributes(
            method,
            TelegramHandlerSymbols.MessageAttribute,
            includeClassRouteAttributes).Count > 0;

        if (hasMessageAttribute)
        {
            routes.Add(new GeneratedRoute(
                GeneratedHandlerKind.Message,
                GeneratedRouteKind.MessageAny,
                Command: null,
                Pattern: null,
                CommandPrefixes: ["/"],
                AllowSpaceAfterPrefix: false,
                IgnoreCase: true,
                textFilters,
                filters,
                ChatMemberTransitions: [],
                RoleRequirements: roleRequirements,
                RouteValues: ImmutableDictionary<string, GeneratedRouteValue>.Empty));
        }
        else
        {
            foreach (GeneratedTextFilter filter in textFilters)
            {
                routes.Add(new GeneratedRoute(
                    GeneratedHandlerKind.Message,
                    GeneratedRouteKind.TextExact,
                    Command: null,
                    Pattern: filter.Value,
                    CommandPrefixes: ["/"],
                    AllowSpaceAfterPrefix: false,
                    filter.IgnoreCase,
                    TextFilters: [filter],
                    filters,
                    ChatMemberTransitions: [],
                    RoleRequirements: roleRequirements,
                    RouteValues: ImmutableDictionary<string, GeneratedRouteValue>.Empty));
            }
        }

        foreach (AttributeData attribute in GetRouteAttributes(
                     method,
                     TelegramHandlerSymbols.TextTemplateAttribute,
                     includeClassRouteAttributes))
        {
            string? template = GetConstructorString(attribute);

            if (template is null ||
                string.IsNullOrWhiteSpace(template) ||
                !TryParseTemplateRouteValues(template, out ImmutableDictionary<string, GeneratedRouteValue> routeValues))
            {
                return [];
            }

            routes.Add(new GeneratedRoute(
                GeneratedHandlerKind.Message,
                GeneratedRouteKind.TextTemplate,
                Command: null,
                Pattern: template.Trim(),
                CommandPrefixes: ["/"],
                AllowSpaceAfterPrefix: false,
                GetNamedBool(attribute, "IgnoreCase", defaultValue: true),
                TextFilters: [],
                filters,
                ChatMemberTransitions: [],
                RoleRequirements: roleRequirements,
                routeValues));
        }

        foreach (AttributeData attribute in GetRouteAttributes(
                     method,
                     TelegramHandlerSymbols.TextRegexAttribute,
                     includeClassRouteAttributes))
        {
            string? pattern = GetConstructorString(attribute);

            if (string.IsNullOrWhiteSpace(pattern) ||
                !TryParseRegexRouteValues(pattern, out ImmutableDictionary<string, GeneratedRouteValue> routeValues))
            {
                return [];
            }

            routes.Add(new GeneratedRoute(
                GeneratedHandlerKind.Message,
                GeneratedRouteKind.TextRegex,
                Command: null,
                Pattern: pattern,
                CommandPrefixes: ["/"],
                AllowSpaceAfterPrefix: false,
                GetNamedBool(attribute, "IgnoreCase", defaultValue: true),
                TextFilters: [],
                filters,
                ChatMemberTransitions: [],
                RoleRequirements: roleRequirements,
                routeValues));
        }

        return routes.ToImmutable();
    }

    private static bool TryBuildFilters(
        IMethodSymbol method,
        out ImmutableArray<GeneratedFilter> filters)
    {
        ImmutableArray<GeneratedFilter>.Builder builder = ImmutableArray.CreateBuilder<GeneratedFilter>();

        if (!AppendFilters(builder, method.ContainingType) ||
            !AppendFilters(builder, method))
        {
            filters = [];
            return false;
        }

        filters = builder.ToImmutable();
        return true;
    }

    private static bool AppendFilters(
        ImmutableArray<GeneratedFilter>.Builder builder,
        ISymbol symbol)
    {
        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(
                     symbol,
                     TelegramHandlerSymbols.ChatTypeAttribute,
                     inherit: true))
        {
            if (attribute.ConstructorArguments.Length == 0 ||
                attribute.ConstructorArguments[0].Values.IsDefaultOrEmpty)
            {
                return false;
            }

            ImmutableArray<string>.Builder values = ImmutableArray.CreateBuilder<string>();

            foreach (TypedConstant value in attribute.ConstructorArguments[0].Values)
            {
                if (value.Value is not int chatType ||
                    !TelegramChatTypeFacts.TryMapToTelegramValue(chatType, out string mappedValue))
                {
                    return false;
                }

                values.Add(mappedValue);
            }

            if (values.Count > 0)
            {
                builder.Add(new GeneratedFilter("ChatType", values.ToImmutable(), LongValues: []));
            }
        }

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(
                     symbol,
                     TelegramHandlerSymbols.ChatIdAttribute,
                     inherit: true))
        {
            ImmutableArray<long> values = attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Values
                    .Select(static value => value.Value)
                    .OfType<long>()
                    .ToImmutableArray()
                : [];

            if (values.Length == 0 ||
                values.Any(static value => value == 0))
            {
                return false;
            }

            builder.Add(new GeneratedFilter("ChatId", StringValues: [], values));
        }

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(
                     symbol,
                     TelegramHandlerSymbols.ChatUsernameAttribute,
                     inherit: true))
        {
            ImmutableArray<string> values = attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Values
                    .Select(static value => value.Value as string)
                    .Select(CanonicalizeChatUsername)
                    .ToImmutableArray()
                : [];

            if (values.Length == 0 ||
                values.Any(static value => value.Length == 0))
            {
                return false;
            }

            builder.Add(new GeneratedFilter("ChatUsername", values, LongValues: []));
        }

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(
                     symbol,
                     TelegramHandlerSymbols.FromUserAttribute,
                     inherit: true))
        {
            ImmutableArray<long> values = attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Values
                    .Select(static value => value.Value)
                    .OfType<long>()
                    .Where(static value => value > 0)
                    .ToImmutableArray()
                : [];

            if (values.Length > 0)
            {
                builder.Add(new GeneratedFilter("FromUser", StringValues: [], values));
            }
        }

        foreach (TelegramBuiltInFilterSpec spec in TelegramBuiltInFilterFacts.MarkerSpecs)
        {
            AppendMarkerFilter(builder, symbol, spec);
        }

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(
                     symbol,
                     TelegramHandlerSymbols.MessageThreadIdAttribute,
                     inherit: true))
        {
            ImmutableArray<long> values = attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Values
                    .Select(static value => value.Value)
                    .OfType<long>()
                    .ToImmutableArray()
                : [];

            if (values.Length == 0 ||
                values.Any(static value => value <= 0))
            {
                return false;
            }

            builder.Add(new GeneratedFilter("MessageThreadId", StringValues: [], values));
        }

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(
                     symbol,
                     TelegramHandlerSymbols.FromBotAttribute,
                     inherit: true))
        {
            bool value = attribute.ConstructorArguments.Length == 0 ||
                         attribute.ConstructorArguments[0].Value is not bool explicitValue ||
                         explicitValue;

            builder.Add(new GeneratedFilter("FromBot", [value.ToString()], LongValues: []));
        }

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(
                     symbol,
                     TelegramHandlerSymbols.CallbackDataPrefixAttribute,
                     inherit: true))
        {
            if (GetConstructorString(attribute) is not { } prefix ||
                string.IsNullOrWhiteSpace(prefix))
            {
                return false;
            }

            builder.Add(new GeneratedFilter("CallbackDataPrefix", [prefix], LongValues: []));
        }

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetGenericAttributes(
                     symbol,
                     TelegramHandlerSymbols.GenericUseFilterAttribute,
                     inherit: true))
        {
            if (attribute.AttributeClass is not { TypeArguments.Length: 1 } filterAttributeType ||
                IsInvalidCustomFilterType(filterAttributeType.TypeArguments[0]) ||
                !TryGetTelegramFilterContextTypes(
                    filterAttributeType.TypeArguments[0],
                    attributeType: null,
                    out ImmutableArray<string> contextMetadataNames))
            {
                return false;
            }

            builder.Add(new GeneratedFilter(
                "Custom",
                StringValues: [],
                LongValues: [],
                filterAttributeType.TypeArguments[0].ToDisplayString(FullyQualifiedFormat),
                CustomContextMetadataNames: contextMetadataNames));
        }

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetTelegramFilterAttributes(symbol, inherit: true))
        {
            if (attribute.AttributeClass is not { IsGenericType: false } attributeType ||
                !TelegramHandlerSymbols.TryGetTelegramFilterAttributeFilterType(attribute, out ITypeSymbol filterType) ||
                IsInvalidCustomFilterType(filterType) ||
                !TryGetTelegramFilterContextTypes(
                    filterType,
                    attributeType,
                    out ImmutableArray<string> contextMetadataNames))
            {
                return false;
            }

            builder.Add(new GeneratedFilter(
                "Custom",
                StringValues: [],
                LongValues: [],
                filterType.ToDisplayString(FullyQualifiedFormat),
                ToAttributeCreationExpression(attribute),
                CustomContextMetadataNames: contextMetadataNames));
        }

        return true;
    }

    private static void AppendMarkerFilter(
        ImmutableArray<GeneratedFilter>.Builder builder,
        ISymbol symbol,
        TelegramBuiltInFilterSpec spec)
    {
        if (TelegramHandlerSymbols.HasAttribute(symbol, spec.AttributeMetadataName, inherit: true))
        {
            builder.Add(new GeneratedFilter(spec.GeneratedKind, StringValues: [], LongValues: []));
        }
    }

    private static bool SupportsGeneratedFilter(
        GeneratedFilter filter,
        GeneratedHandlerKind handlerKind)
    {
        return TelegramBuiltInFilterFacts.TryGetSpecByGeneratedKind(filter.Kind, out TelegramBuiltInFilterSpec spec) &&
               TelegramBuiltInFilterFacts.SupportsRouteKind(spec.Target, ToMetadataRouteKind(handlerKind));
    }

    private static bool TryPrepareCustomFilters(
        ImmutableArray<GeneratedFilter> filters,
        GeneratedHandlerKind handlerKind,
        out ImmutableArray<GeneratedFilter> preparedFilters)
    {
        ImmutableArray<GeneratedFilter>.Builder builder = ImmutableArray.CreateBuilder<GeneratedFilter>(filters.Length);

        foreach (GeneratedFilter filter in filters)
        {
            if (filter.CustomTypeName is null)
            {
                builder.Add(filter);
                continue;
            }

            if (!TryResolveCustomFilterContextType(filter, handlerKind, out string contextTypeName))
            {
                preparedFilters = [];
                return false;
            }

            builder.Add(filter with { CustomContextTypeName = contextTypeName });
        }

        preparedFilters = builder.ToImmutable();
        return true;
    }

    private static bool TryResolveCustomFilterContextType(
        GeneratedFilter filter,
        GeneratedHandlerKind handlerKind,
        out string contextTypeName)
    {
        string expectedContext = handlerKind switch
        {
            GeneratedHandlerKind.Callback => TelegramHandlerSymbols.CallbackQueryContext,
            GeneratedHandlerKind.ChatMember => TelegramHandlerSymbols.ChatMemberUpdatedContext,
            _ => TelegramHandlerSymbols.MessageContext
        };

        if (filter.CustomContextMetadataNames.Contains(expectedContext, StringComparer.Ordinal))
        {
            contextTypeName = ToFullyQualifiedTypeName(expectedContext);
            return true;
        }

        if (filter.CustomContextMetadataNames.Contains(TelegramHandlerSymbols.TelegramUpdateContext, StringComparer.Ordinal))
        {
            contextTypeName = ToFullyQualifiedTypeName(TelegramHandlerSymbols.TelegramUpdateContext);
            return true;
        }

        contextTypeName = string.Empty;
        return false;
    }

    private static string ToFullyQualifiedTypeName(string metadataName)
    {
        return $"global::{metadataName}";
    }

    private static string CanonicalizeChatUsername(string? username)
    {
        if (username is null)
        {
            return string.Empty;
        }

        string value = username.Trim();

        if (value.StartsWith("@", StringComparison.Ordinal))
        {
            return value.Substring(1);
        }

        return value;
    }

    private static bool HasChatMemberTransitionAttributes(IMethodSymbol method)
    {
        return TelegramHandlerSymbols.HasAttribute(method.ContainingType, TelegramHandlerSymbols.ChatMemberTransitionAttribute, inherit: true) ||
               TelegramHandlerSymbols.HasAttribute(method, TelegramHandlerSymbols.ChatMemberTransitionAttribute, inherit: true) ||
               TelegramHandlerSymbols.HasAttribute(method.ContainingType, TelegramHandlerSymbols.ChatMemberChangedAttribute, inherit: true) ||
               TelegramHandlerSymbols.HasAttribute(method, TelegramHandlerSymbols.ChatMemberChangedAttribute, inherit: true);
    }

    private static bool TryBuildRoleRequirements(
        IMethodSymbol method,
        out ImmutableArray<int> roleRequirements)
    {
        ImmutableArray<int>.Builder builder = ImmutableArray.CreateBuilder<int>();

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(method.ContainingType, TelegramHandlerSymbols.RequireTelegramRoleAttribute, inherit: true)
                     .Concat(TelegramHandlerSymbols.GetAttributes(method, TelegramHandlerSymbols.RequireTelegramRoleAttribute, inherit: true)))
        {
            if (!TelegramMemberStatusFacts.TryGetRoleRequirementMask(attribute, out int allowedStatuses) ||
                !TelegramMemberStatusFacts.IsValid(allowedStatuses))
            {
                roleRequirements = [];
                return false;
            }

            builder.Add(allowedStatuses);
        }

        roleRequirements = builder.ToImmutable();
        return true;
    }

    private static bool TryBuildChatMemberTransitions(
        IMethodSymbol method,
        out ImmutableArray<GeneratedChatMemberTransition> transitions)
    {
        ImmutableArray<GeneratedChatMemberTransition>.Builder builder = ImmutableArray.CreateBuilder<GeneratedChatMemberTransition>();

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(method.ContainingType, TelegramHandlerSymbols.ChatMemberTransitionAttribute, inherit: true)
                     .Concat(TelegramHandlerSymbols.GetAttributes(method, TelegramHandlerSymbols.ChatMemberTransitionAttribute, inherit: true)))
        {
            if (attribute.ConstructorArguments.Length == 0 ||
                attribute.ConstructorArguments[0].Value is not int transition ||
                !TelegramMemberStatusFacts.TryMapTransition(transition, out int oldStatus, out int newStatus))
            {
                transitions = [];
                return false;
            }

            builder.Add(new GeneratedChatMemberTransition(oldStatus, newStatus));
        }

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(method.ContainingType, TelegramHandlerSymbols.ChatMemberChangedAttribute, inherit: true)
                     .Concat(TelegramHandlerSymbols.GetAttributes(method, TelegramHandlerSymbols.ChatMemberChangedAttribute, inherit: true)))
        {
            if (attribute.ConstructorArguments.Length < 2 ||
                attribute.ConstructorArguments[0].Value is not int oldStatus ||
                attribute.ConstructorArguments[1].Value is not int newStatus ||
                !TelegramMemberStatusFacts.IsValid(oldStatus) ||
                !TelegramMemberStatusFacts.IsValid(newStatus))
            {
                transitions = [];
                return false;
            }

            builder.Add(new GeneratedChatMemberTransition(oldStatus, newStatus));
        }

        transitions = builder.ToImmutable();
        return true;
    }

    private static TelegramHandlerMetadataRouteKind ToMetadataRouteKind(GeneratedHandlerKind handlerKind)
    {
        return handlerKind switch
        {
            GeneratedHandlerKind.Command => TelegramHandlerMetadataRouteKind.Command,
            GeneratedHandlerKind.Message => TelegramHandlerMetadataRouteKind.Message,
            GeneratedHandlerKind.Callback => TelegramHandlerMetadataRouteKind.Callback,
            GeneratedHandlerKind.ChatMember => TelegramHandlerMetadataRouteKind.ChatMember,
            _ => TelegramHandlerMetadataRouteKind.Message
        };
    }

    private static bool IsInvalidCustomFilterType(ITypeSymbol type)
    {
        return IsInvalidConcreteNamedType(type);
    }

    private static bool TryGetTelegramFilterContextTypes(
        ITypeSymbol type,
        ITypeSymbol? attributeType,
        out ImmutableArray<string> contextTypes)
    {
        ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();

        foreach (INamedTypeSymbol candidate in type.AllInterfaces)
        {
            if (attributeType is null)
            {
                if (string.Equals(
                        candidate.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        TelegramHandlerSymbols.GenericTelegramFilter,
                        StringComparison.Ordinal) &&
                    candidate.TypeArguments.Length == 1)
                {
                    builder.Add(candidate.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
                }

                continue;
            }

            if (string.Equals(
                    candidate.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    TelegramHandlerSymbols.GenericParameterizedTelegramFilter,
                    StringComparison.Ordinal) &&
                candidate.TypeArguments.Length == 2 &&
                SymbolEqualityComparer.Default.Equals(candidate.TypeArguments[1], attributeType))
            {
                builder.Add(candidate.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
            }
        }

        contextTypes = builder.ToImmutable();
        return contextTypes.Length > 0;
    }

    private static string? GetModuleName(INamedTypeSymbol handlerType)
    {
        AttributeData? attribute = TelegramHandlerSymbols.GetFirstAttribute(
            handlerType,
            TelegramHandlerSymbols.TelegramModuleAttribute);

        if (attribute?.ConstructorArguments.Length > 0 &&
            attribute.ConstructorArguments[0].Value is string name &&
            !string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        return null;
    }

    private static bool TryGetExpectedContextType(GeneratedHandlerKind kind, out string contextType)
    {
        contextType = kind switch
        {
            GeneratedHandlerKind.Callback => TelegramHandlerSymbols.CallbackQueryContext,
            GeneratedHandlerKind.ChatMember => TelegramHandlerSymbols.ChatMemberUpdatedContext,
            _ => TelegramHandlerSymbols.MessageContext
        };
        return true;
    }

    private static bool HasExactlyOneExpectedContext(IMethodSymbol method, string expectedContextType)
    {
        IParameterSymbol[] contextParameters = method.Parameters
            .Where(static parameter =>
                TelegramHandlerSymbols.IsType(parameter.Type, TelegramHandlerSymbols.MessageContext) ||
                TelegramHandlerSymbols.IsType(parameter.Type, TelegramHandlerSymbols.CallbackQueryContext) ||
                TelegramHandlerSymbols.IsType(parameter.Type, TelegramHandlerSymbols.ChatMemberUpdatedContext) ||
                TelegramHandlerSymbols.IsType(parameter.Type, TelegramHandlerSymbols.TelegramUpdateContext))
            .ToArray();

        return contextParameters.Length == 1 &&
               TelegramHandlerSymbols.IsType(contextParameters[0].Type, expectedContextType);
    }

    private static bool HasValidCallbackPayloadParameter(
        IMethodSymbol method,
        ITypeSymbol? callbackPayloadType)
    {
        if (callbackPayloadType is null)
        {
            return true;
        }

        if (IsInvalidCallbackPayloadType(callbackPayloadType))
        {
            return false;
        }

        return method.Parameters.Count(parameter =>
            SymbolEqualityComparer.Default.Equals(parameter.Type, callbackPayloadType)) == 1;
    }

    private static bool IsInvalidCallbackPayloadType(ITypeSymbol type)
    {
        return IsInvalidConcreteNamedType(type);
    }

    private static bool IsInvalidConcreteNamedType(ITypeSymbol type)
    {
        return type.TypeKind is TypeKind.Interface or TypeKind.TypeParameter ||
               type is not INamedTypeSymbol namedType ||
               namedType.IsAbstract ||
               namedType.IsUnboundGenericType ||
               namedType.TypeArguments.Any(static argument => argument.TypeKind == TypeKind.TypeParameter);
    }

    private static bool IsSupportedReturnType(ITypeSymbol returnType)
    {
        return TelegramHandlerSymbols.IsType(returnType, TelegramHandlerSymbols.Task) ||
               TelegramHandlerSymbols.IsType(returnType, TelegramHandlerSymbols.ValueTask);
    }

    private static bool TryGetErrorReturnKind(
        ITypeSymbol returnType,
        out GeneratedErrorReturnKind returnKind)
    {
        if (TelegramHandlerSymbols.IsType(returnType, TelegramHandlerSymbols.TelegramErrorHandlingResult))
        {
            returnKind = GeneratedErrorReturnKind.Sync;
            return true;
        }

        if (returnType is INamedTypeSymbol
            {
                IsGenericType: true,
                TypeArguments.Length: 1
            } namedType &&
            TelegramHandlerSymbols.IsType(namedType.TypeArguments[0], TelegramHandlerSymbols.TelegramErrorHandlingResult))
        {
            GeneratedErrorReturnKind? matchedReturnKind = namedType.ConstructedFrom
                .ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) switch
            {
                "System.Threading.Tasks.Task<TResult>" => GeneratedErrorReturnKind.Task,
                "System.Threading.Tasks.ValueTask<TResult>" => GeneratedErrorReturnKind.ValueTask,
                _ => null
            };

            if (matchedReturnKind is not null)
            {
                returnKind = matchedReturnKind.Value;
                return true;
            }
        }

        returnKind = default;
        return false;
    }

    private static bool IsValidCommand(string? command)
    {
        return command is not null &&
               !string.IsNullOrWhiteSpace(command) &&
               !command.StartsWith("/", StringComparison.Ordinal) &&
               !command.Contains("@", StringComparison.Ordinal) &&
               !command.Any(char.IsWhiteSpace);
    }

    private static string? GetConstructorString(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length > 0 &&
            attribute.ConstructorArguments[0].Value is string value)
        {
            return value.Trim();
        }

        return null;
    }

    private static bool TryGetPrefixes(
        AttributeData attribute,
        out ImmutableArray<string> prefixes)
    {
        prefixes = ["/"];

        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (!string.Equals(argument.Key, "Prefixes", StringComparison.Ordinal))
            {
                continue;
            }

            if (argument.Value.Values.IsDefaultOrEmpty)
            {
                return false;
            }

            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (TypedConstant value in argument.Value.Values)
            {
                if (value.Value is not string prefix ||
                    string.IsNullOrWhiteSpace(prefix))
                {
                    return false;
                }

                string normalized = prefix.Trim();

                if (seen.Add(normalized))
                {
                    builder.Add(normalized);
                }
            }

            prefixes = builder.ToImmutable();
            return true;
        }

        return true;
    }

    private static bool CommandPatternStartsWithPrefix(
        string pattern,
        ImmutableArray<string> prefixes)
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

    private static bool TryParseTemplateRouteValues(
        string? template,
        out ImmutableDictionary<string, GeneratedRouteValue> routeValues)
    {
        routeValues = ImmutableDictionary<string, GeneratedRouteValue>.Empty;

        if (template is null ||
            string.IsNullOrWhiteSpace(template))
        {
            return false;
        }

        ImmutableDictionary<string, GeneratedRouteValue>.Builder values = ImmutableDictionary.CreateBuilder<string, GeneratedRouteValue>(StringComparer.Ordinal);
        Regex placeholderRegex = new Regex(
            @"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:(?<nameOptional>\?)|:(?<constraint>[A-Za-z][A-Za-z0-9_]*)(?<constraintOptional>\?)?)?\}",
            RegexOptions.CultureInvariant);
        int position = 0;

        foreach (Match match in placeholderRegex.Matches(template))
        {
            if (match.Index != position &&
                ContainsBrace(template.Substring(position, match.Index - position)))
            {
                return false;
            }

            string name = match.Groups["name"].Value;
            bool hasNameOptional = match.Groups["nameOptional"].Success;
            bool hasConstraintOptional = match.Groups["constraintOptional"].Success;
            string constraint = match.Groups["constraint"].Success
                ? match.Groups["constraint"].Value
                : "string";

            if (values.ContainsKey(name) ||
                constraint is not ("string" or "int" or "long"))
            {
                return false;
            }

            values.Add(name, new GeneratedRouteValue(constraint, hasNameOptional || hasConstraintOptional));
            position = match.Index + match.Length;
        }

        if (ContainsBrace(template.Substring(position)))
        {
            return false;
        }

        routeValues = values.ToImmutable();
        return true;
    }

    private static bool TryParseRegexRouteValues(
        string? pattern,
        out ImmutableDictionary<string, GeneratedRouteValue> routeValues)
    {
        routeValues = ImmutableDictionary<string, GeneratedRouteValue>.Empty;

        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        Regex regex;

        try
        {
            regex = new Regex(pattern, RegexOptions.CultureInvariant);
        }
        catch (ArgumentException)
        {
            return false;
        }

        ImmutableDictionary<string, GeneratedRouteValue>.Builder values = ImmutableDictionary.CreateBuilder<string, GeneratedRouteValue>(StringComparer.Ordinal);

        foreach (string name in regex.GetGroupNames())
        {
            if (!int.TryParse(name, out _))
            {
                values[name] = new GeneratedRouteValue(Constraint: null, IsOptional: false);
            }
        }

        routeValues = values.ToImmutable();
        return true;
    }

    private static bool ContainsBrace(string value)
    {
        return value.IndexOf("{", StringComparison.Ordinal) >= 0 ||
               value.IndexOf("}", StringComparison.Ordinal) >= 0;
    }

    private static bool TryResolveRouteValueNames(
        IMethodSymbol method,
        ImmutableArray<GeneratedRoute> routes,
        out ImmutableHashSet<string> routeValueNames)
    {
        routeValueNames = ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);

        GeneratedRoute[] routeValueRoutes = routes
            .Where(static route => route.RouteValues.Count > 0)
            .ToArray();

        if (routeValueRoutes.Length == 0)
        {
            return true;
        }

        if (routes.Any(static route => route.RouteValues.Count == 0))
        {
            return false;
        }

        string[] firstNames = routeValueRoutes[0].RouteValues.Keys.OrderBy(static name => name, StringComparer.Ordinal).ToArray();

        foreach (GeneratedRoute route in routeValueRoutes.Skip(1))
        {
            string[] names = route.RouteValues.Keys.OrderBy(static name => name, StringComparer.Ordinal).ToArray();

            if (!firstNames.SequenceEqual(names, StringComparer.Ordinal))
            {
                return false;
            }
        }

        ImmutableHashSet<string>.Builder builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);

        foreach (string name in firstNames)
        {
            IParameterSymbol? parameter = method.Parameters.FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal));

            if (parameter is null ||
                !IsSupportedRouteValueParameterType(parameter.Type))
            {
                return false;
            }

            foreach (GeneratedRoute route in routeValueRoutes)
            {
                GeneratedRouteValue routeValue = route.RouteValues[name];

                if (routeValue.IsOptional != IsNullableRouteValueParameter(parameter.Type))
                {
                    return false;
                }

                if (routeValue.Constraint is not null &&
                    !RouteConstraintMatchesParameter(routeValue.Constraint, parameter.Type))
                {
                    return false;
                }
            }

            builder.Add(name);
        }

        routeValueNames = builder.ToImmutable();
        return true;
    }

    private static bool IsSupportedRouteValueParameterType(ITypeSymbol type)
    {
        return IsRouteValueParameterType(type);
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

    private static string GetRouteValueTypeName(GeneratedRouteValue routeValue)
    {
        return routeValue.Constraint switch
        {
            "int" => "int",
            "long" => "long",
            _ => "string"
        };
    }

    private static ImmutableArray<GeneratedTextFilter> BuildTextFilters(
        IMethodSymbol method,
        bool includeClassRouteAttributes)
    {
        ImmutableArray<GeneratedTextFilter>.Builder builder = ImmutableArray.CreateBuilder<GeneratedTextFilter>();

        foreach (AttributeData attribute in GetRouteAttributes(
                     method,
                     TelegramHandlerSymbols.TextAttribute,
                     includeClassRouteAttributes))
        {
            string? value = attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value as string
                : null;

            if (value is null ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            int mode = attribute.ConstructorArguments.Length > 1 &&
                       attribute.ConstructorArguments[1].Value is int modeValue
                ? modeValue
                : 0;
            bool ignoreCase = attribute.ConstructorArguments.Length > 2 &&
                             attribute.ConstructorArguments[2].Value is bool ignoreCaseValue
                ? ignoreCaseValue
                : true;

            builder.Add(new GeneratedTextFilter(value, mode, ignoreCase));
        }

        return builder.ToImmutable();
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

    private static bool IsClassBasedHandlerType(INamedTypeSymbol type)
    {
        return IsAssignableTo(type, TelegramHandlerSymbols.MessageHandler) ||
               IsAssignableTo(type, TelegramHandlerSymbols.CallbackHandler) ||
               IsAssignableTo(type, TelegramHandlerSymbols.ChatMemberUpdateHandler);
    }

    private static bool IsClassBasedRouteCompatible(
        INamedTypeSymbol type,
        GeneratedHandlerKind kind,
        ITypeSymbol? callbackPayloadType)
    {
        if (!IsClassBasedHandlerType(type))
        {
            return true;
        }

        if (IsAssignableTo(type, TelegramHandlerSymbols.MessageHandler))
        {
            return kind is GeneratedHandlerKind.Command or GeneratedHandlerKind.Message;
        }

        if (TryGetCallbackHandlerPayloadType(type, out ITypeSymbol handlerPayloadType))
        {
            return kind == GeneratedHandlerKind.Callback &&
                   callbackPayloadType is not null &&
                   SymbolEqualityComparer.Default.Equals(handlerPayloadType, callbackPayloadType);
        }

        if (IsDirectlyAssignableTo(type, TelegramHandlerSymbols.CallbackHandler))
        {
            return kind == GeneratedHandlerKind.Callback &&
                   callbackPayloadType is null;
        }

        return IsAssignableTo(type, TelegramHandlerSymbols.ChatMemberUpdateHandler) &&
               kind == GeneratedHandlerKind.ChatMember;
    }

    private static ImmutableArray<IMethodSymbol> GetDeclaredHandleAsyncMethods(INamedTypeSymbol type)
    {
        return type.GetMembers("HandleAsync")
            .OfType<IMethodSymbol>()
            .Where(static method =>
                method.DeclaredAccessibility == Accessibility.Public &&
                !method.IsStatic &&
                method.MethodKind == MethodKind.Ordinary &&
                !method.IsGenericMethod)
            .ToImmutableArray();
    }

    private static bool TryGetCallbackHandlerPayloadType(
        INamedTypeSymbol type,
        out ITypeSymbol payloadType)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ==
                TelegramHandlerSymbols.GenericCallbackHandler &&
                current.TypeArguments.Length == 1)
            {
                payloadType = current.TypeArguments[0];
                return true;
            }
        }

        payloadType = null!;
        return false;
    }

    private static bool IsAssignableTo(INamedTypeSymbol type, string metadataName)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == metadataName ||
                current.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDirectlyAssignableTo(INamedTypeSymbol type, string metadataName)
    {
        return IsAssignableTo(type, metadataName) &&
               !TryGetCallbackHandlerPayloadType(type, out _);
    }

    private static bool TryBuildStates(
        IMethodSymbol method,
        out ImmutableArray<string> states,
        out string? sceneName)
    {
        ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();
        states = [];
        sceneName = null;

        foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(method.ContainingType, TelegramHandlerSymbols.StateAttribute, inherit: true)
                     .Concat(TelegramHandlerSymbols.GetAttributes(method, TelegramHandlerSymbols.StateAttribute, inherit: true)))
        {
            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string state &&
                !string.IsNullOrWhiteSpace(state))
            {
                builder.Add(state);
            }
        }

        AttributeData[] hasGenericStateAttributes = GetGenericStateAttributes(method.ContainingType)
            .Concat(GetGenericStateAttributes(method))
            .ToArray();

        foreach (AttributeData attribute in hasGenericStateAttributes)
        {
            if (TryResolveTypedState(attribute, out string state))
            {
                builder.Add(state);
            }
        }

        AttributeData? sceneStep = TelegramHandlerSymbols.GetFirstAttribute(
            method,
            TelegramHandlerSymbols.SceneStepAttribute,
            inherit: true);

        if (sceneStep is not null)
        {
            if (builder.Count > 0 ||
                !TryResolveSceneStep(method, sceneStep, out string sceneState, out sceneName))
            {
                return false;
            }

            builder.Add(sceneState);
        }

        states = builder.ToImmutable();
        return true;
    }

    private static IEnumerable<AttributeData> GetGenericStateAttributes(ISymbol symbol)
    {
        return TelegramHandlerSymbols.GetGenericAttributes(
            symbol,
            TelegramHandlerSymbols.GenericStateAttribute,
            inherit: true);
    }

    private static bool TryResolveTypedState(AttributeData attribute, out string state)
    {
        state = string.Empty;

        if (attribute.AttributeClass is not { TypeArguments.Length: 1 } attributeType ||
            attribute.ConstructorArguments.Length == 0 ||
            attribute.ConstructorArguments[0].Value is not string stateName ||
            string.IsNullOrWhiteSpace(stateName) ||
            attributeType.TypeArguments[0] is not INamedTypeSymbol groupType)
        {
            return false;
        }

        if (!TryGetStateContainerPrefix(groupType, out string prefix))
        {
            return false;
        }

        IPropertySymbol? property = groupType
            .GetMembers(stateName)
            .OfType<IPropertySymbol>()
            .FirstOrDefault(IsUsableStateProperty);

        if (property is null || !IsPartialStateProperty(property))
        {
            return false;
        }

        state = $"{prefix}:{GetStateSegment(property)}";
        return true;
    }

    private static bool TryResolveSceneStep(
        IMethodSymbol method,
        AttributeData attribute,
        out string state,
        out string? sceneName)
    {
        state = string.Empty;
        sceneName = null;

        if (!TryGetScenePrefix(method.ContainingType, out string prefix) ||
            attribute.ConstructorArguments.Length == 0 ||
            attribute.ConstructorArguments[0].Value is not string stateName ||
            string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        IPropertySymbol? property = method.ContainingType
            .GetMembers(stateName)
            .OfType<IPropertySymbol>()
            .FirstOrDefault(IsUsableStateProperty);

        if (property is null || !IsPartialStateProperty(property))
        {
            return false;
        }

        sceneName = prefix;
        state = $"{prefix}:{GetStateSegment(property)}";
        return true;
    }

    private static bool TryGetStateContainerPrefix(INamedTypeSymbol type, out string prefix)
    {
        return TryGetStateGroupPrefix(type, out prefix) ||
               TryGetScenePrefix(type, out prefix);
    }

    private static bool TryGetStateGroupPrefix(INamedTypeSymbol type, out string prefix)
    {
        return TryGetAttributeStringPrefix(type, TelegramHandlerSymbols.StateGroupAttribute, out prefix);
    }

    private static bool TryGetScenePrefix(INamedTypeSymbol type, out string prefix)
    {
        return TryGetAttributeStringPrefix(type, TelegramHandlerSymbols.SceneAttribute, out prefix);
    }

    private static bool TryGetAttributeStringPrefix(
        INamedTypeSymbol type,
        string metadataName,
        out string prefix)
    {
        AttributeData? attribute = TelegramHandlerSymbols.GetFirstAttribute(type, metadataName);

        if (attribute?.ConstructorArguments.Length > 0 &&
            attribute.ConstructorArguments[0].Value is string value &&
            !string.IsNullOrWhiteSpace(value))
        {
            prefix = value;
            return true;
        }

        prefix = string.Empty;
        return false;
    }

    private static string GetStateSegment(IPropertySymbol property)
    {
        AttributeData? attribute = TelegramHandlerSymbols.GetFirstAttribute(
            property,
            TelegramHandlerSymbols.StateValueAttribute);

        if (attribute?.ConstructorArguments.Length > 0 &&
            attribute.ConstructorArguments[0].Value is string value &&
            !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return ToCamelCase(property.Name);
    }

    private static bool IsUsableStateProperty(IPropertySymbol property)
    {
        return property.DeclaredAccessibility == Accessibility.Public &&
               property.IsStatic &&
               TelegramHandlerSymbols.IsType(property.Type, TelegramHandlerSymbols.State);
    }

    private static bool IsPartialStateProperty(IPropertySymbol property)
    {
        return property
            .DeclaringSyntaxReferences
            .Select(static reference => reference.GetSyntax())
            .OfType<PropertyDeclarationSyntax>()
            .Any(static declaration => declaration.Modifiers.Any(modifier => modifier.Text == "partial"));
    }

    private static string ToCamelCase(string value)
    {
        return value.Length == 0
            ? value
            : char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    private static ImmutableArray<GeneratedParameter> BuildParameters(
        IMethodSymbol method,
        GeneratedHandlerKind kind,
        string expectedContextType,
        ITypeSymbol? callbackPayloadType,
        ImmutableHashSet<string> routeValueNames)
    {
        ImmutableArray<GeneratedParameter>.Builder builder = ImmutableArray.CreateBuilder<GeneratedParameter>();

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            GeneratedParameterKind parameterKind = GetParameterKind(parameter, kind, expectedContextType, callbackPayloadType, routeValueNames);

            builder.Add(new GeneratedParameter(
                parameter.Type.ToDisplayString(FullyQualifiedFormat),
                parameterKind,
                parameter.Name));
        }

        return builder.ToImmutable();
    }

    private static GeneratedParameterKind GetParameterKind(
        IParameterSymbol parameter,
        GeneratedHandlerKind kind,
        string expectedContextType,
        ITypeSymbol? callbackPayloadType,
        ImmutableHashSet<string> routeValueNames)
    {
        if (TelegramHandlerSymbols.IsType(parameter.Type, expectedContextType))
        {
            return GeneratedParameterKind.Context;
        }

        if (TelegramHandlerSymbols.IsType(parameter.Type, TelegramHandlerSymbols.CancellationToken))
        {
            return GeneratedParameterKind.CancellationToken;
        }

        if (routeValueNames.Contains(parameter.Name))
        {
            return GeneratedParameterKind.RouteValue;
        }

        if (kind == GeneratedHandlerKind.Callback &&
            callbackPayloadType is not null &&
            SymbolEqualityComparer.Default.Equals(parameter.Type, callbackPayloadType))
        {
            return GeneratedParameterKind.CallbackPayload;
        }

        return GeneratedParameterKind.Service;
    }

    private static ImmutableArray<GeneratedErrorParameter> BuildErrorParameters(IMethodSymbol method)
    {
        ImmutableArray<GeneratedErrorParameter>.Builder builder = ImmutableArray.CreateBuilder<GeneratedErrorParameter>();

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            builder.Add(new GeneratedErrorParameter(
                parameter.Type.ToDisplayString(FullyQualifiedFormat),
                GetErrorParameterKind(parameter),
                parameter.Name));
        }

        return builder.ToImmutable();
    }

    private static GeneratedErrorParameterKind GetErrorParameterKind(IParameterSymbol parameter)
    {
        if (TelegramHandlerSymbols.IsType(parameter.Type, TelegramHandlerSymbols.TelegramErrorContext))
        {
            return GeneratedErrorParameterKind.ErrorContext;
        }

        if (TelegramHandlerSymbols.IsType(parameter.Type, TelegramHandlerSymbols.CancellationToken))
        {
            return GeneratedErrorParameterKind.CancellationToken;
        }

        if (IsExceptionType(parameter.Type))
        {
            return GeneratedErrorParameterKind.Exception;
        }

        if (IsTelegramContextType(parameter.Type))
        {
            return GeneratedErrorParameterKind.TelegramContext;
        }

        if (IsErrorRouteValueParameterType(parameter.Type))
        {
            return GeneratedErrorParameterKind.RouteValue;
        }

        return GeneratedErrorParameterKind.Service;
    }

    private static bool IsExceptionType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               IsAssignableTo(namedType, TelegramHandlerSymbols.Exception);
    }

    private static bool IsTelegramContextType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               IsAssignableTo(namedType, TelegramHandlerSymbols.TelegramUpdateContext);
    }

    private static bool IsAssignableFrom(ITypeSymbol targetType, ITypeSymbol sourceType)
    {
        if (sourceType is not INamedTypeSymbol source)
        {
            return false;
        }

        for (INamedTypeSymbol? current = source; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, targetType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsErrorRouteValueParameterType(ITypeSymbol type)
    {
        return IsRouteValueParameterType(type);
    }

    private static bool IsRouteValueParameterType(ITypeSymbol type)
    {
        return type.SpecialType is
                   SpecialType.System_String or
                   SpecialType.System_Int32 or
                   SpecialType.System_Int64 ||
               type is INamedTypeSymbol
               {
                   OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                   TypeArguments.Length: 1
               } nullable &&
               nullable.TypeArguments[0].SpecialType is
                   SpecialType.System_Int32 or
                   SpecialType.System_Int64;
    }
}
