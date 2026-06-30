using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TeleFlow.Generators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed partial class TelegramHandlerAnalyzer : DiagnosticAnalyzer
{
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            ConcurrentBag<CommandRegistration> commands = new ConcurrentBag<CommandRegistration>();
            ConcurrentBag<CallbackPrefixRegistration> callbackPrefixes = new ConcurrentBag<CallbackPrefixRegistration>();

            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeMethod(symbolContext, commands),
                SymbolKind.Method);

            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext, commands, callbackPrefixes),
                SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(context =>
            {
                foreach (IGrouping<string, CommandRegistration> duplicateGroup in commands
                             .GroupBy(static command => command.Key, StringComparer.Ordinal)
                             .Where(static group => group.Count() > 1))
                {
                    foreach (CommandRegistration registration in duplicateGroup)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DuplicateCommand,
                            registration.Location,
                            registration.Display));
                    }
                }

                foreach (IGrouping<string, CallbackPrefixRegistration> duplicateGroup in callbackPrefixes
                             .GroupBy(static registration => registration.Prefix, StringComparer.Ordinal)
                             .Where(static group => group.Count() > 1))
                {
                    foreach (CallbackPrefixRegistration registration in duplicateGroup)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DuplicateCallbackDataPrefix,
                            registration.Location,
                            duplicateGroup.Key));
                    }
                }
            });
        });
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        ConcurrentBag<CommandRegistration> commands,
        ConcurrentBag<CallbackPrefixRegistration> callbackPrefixes)
    {
        INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;

        AnalyzeCallbackDataType(context, type, callbackPrefixes);
        AnalyzeStateGroupType(context, type);
        AnalyzeSceneType(context, type);
        AnalyzeTelegramModuleType(context, type);
        AnalyzeClassLevelRouteUsage(context, type);
        AnalyzeClassBasedHandlerType(context, type, commands);
        AnalyzeInheritedHandlerMethods(context, type);
    }

    private static void AnalyzeMethod(
        SymbolAnalysisContext context,
        ConcurrentBag<CommandRegistration> commands)
    {
        IMethodSymbol method = (IMethodSymbol)context.Symbol;

        AnalyzeErrorHandlerMethod(context, method);

        if (IsClassBasedHandlerType(method.ContainingType))
        {
            return;
        }

        AnalyzeHandlerMethod(context, method, commands, includeClassRouteAttributes: false);
    }

    private static void AnalyzeHandlerMethod(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        ConcurrentBag<CommandRegistration> commands,
        bool includeClassRouteAttributes)
    {
        bool hasRouteAttribute = HasAnyRouteAttribute(method, includeClassRouteAttributes);
        bool hasSceneStep = TelegramHandlerSymbols.HasAttribute(
            method,
            TelegramHandlerSymbols.SceneStepAttribute,
            inherit: true);

        if (!hasRouteAttribute)
        {
            if (hasSceneStep)
            {
                Location? sceneStepLocation = method.Locations.FirstOrDefault(static candidate => candidate.IsInSource);

                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidSceneStep,
                    sceneStepLocation,
                    "SceneStepAttribute requires an explicit Telegram route attribute."));
            }

            AnalyzeMissingRouteAttribute(context, method);
            AnalyzeAutoAnswerCallback(context, method, routeKind: null);
            return;
        }

        HandlerKind? routeKind = GetRouteKind(method, includeClassRouteAttributes, out ITypeSymbol? callbackPayloadType, out bool hasRawCallback, out bool hasMixedCallbackRoutes);
        string displayName = method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        Location? location = method.Locations.FirstOrDefault(static candidate => candidate.IsInSource);

        if (method.ContainingType.TypeKind is TypeKind.Interface)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidHandlerType,
                location,
                method.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
        }

        if (method.DeclaredAccessibility != Accessibility.Public || method.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidHandlerMethod,
                location,
                displayName));
        }

        if (hasMixedCallbackRoutes || routeKind is null)
        {
            if ((hasRawCallback || callbackPayloadType is not null) &&
                HasAnyMessageRouteAttribute(method, includeClassRouteAttributes))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    TextOnCallback,
                    location));
            }

            context.ReportDiagnostic(Diagnostic.Create(
                MultipleRouteAttributes,
                location,
                displayName));
            return;
        }

        if (!IsSupportedReturnType(method.ReturnType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                UnsupportedReturnType,
                location,
                displayName));
        }

        ValidateContextParameter(context, method, routeKind.Value, displayName, location);
        ValidateCancellationTokens(context, method, displayName, location);
        ValidateTypedStateAttributes(context, method.ContainingType);
        ValidateTypedStateAttributes(context, method);
        AnalyzeSceneStep(context, method, location);
        AnalyzeAutoAnswerCallback(context, method, routeKind.Value);

        AnalyzeCommandRoutes(context, method, commands, displayName, location, includeClassRouteAttributes);
        AnalyzeTemplateAndRegexRoutes(context, method, routeKind.Value, location, includeClassRouteAttributes);
        AnalyzeBuiltInFilters(context, method, routeKind.Value, location);
        AnalyzeCustomFilters(context, method, routeKind.Value, location);
        AnalyzeChatMemberTransitions(context, method, routeKind.Value, location);
        AnalyzeTelegramRoleRequirements(context, method, location);

        if (routeKind.Value == HandlerKind.Callback &&
            HasAnyMessageRouteAttribute(method, includeClassRouteAttributes))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TextOnCallback,
                location));
        }

        if (routeKind.Value == HandlerKind.Callback)
        {
            ValidateCallbackRoute(context, method, callbackPayloadType, hasRawCallback, location);
        }
    }

    private static void AnalyzeAutoAnswerCallback(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        HandlerKind? routeKind)
    {
        if (!TelegramHandlerSymbols.HasAttribute(
                method,
                TelegramHandlerSymbols.AutoAnswerCallbackAttribute,
                inherit: true))
        {
            return;
        }

        if (routeKind == HandlerKind.Callback)
        {
            return;
        }

        Location? location = method.Locations.FirstOrDefault(static candidate => candidate.IsInSource);
        context.ReportDiagnostic(Diagnostic.Create(
            InvalidAutoAnswerCallback,
            location,
            "AutoAnswerCallbackAttribute can be used only on callback handlers."));
    }

}
