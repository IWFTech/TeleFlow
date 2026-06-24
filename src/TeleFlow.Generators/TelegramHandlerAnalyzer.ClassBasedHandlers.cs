using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TeleFlow.Generators;

public sealed partial class TelegramHandlerAnalyzer
{
    private static void AnalyzeClassLevelRouteUsage(
        SymbolAnalysisContext context,
        INamedTypeSymbol type)
    {
        if (IsClassBasedHandlerType(type) ||
            !TelegramHandlerSymbols.HasAnyRouteAttribute(type))
        {
            return;
        }

        Location? location = type.Locations.FirstOrDefault(static candidate => candidate.IsInSource);
        string typeName = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

        context.ReportDiagnostic(Diagnostic.Create(
            InvalidClassBasedHandler,
            location,
            $"Telegram handler type '{typeName}' declares class-level route metadata but does not derive from a TeleFlow class-based handler type."));
    }

    private static void AnalyzeClassBasedHandlerType(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        ConcurrentBag<CommandRegistration> commands)
    {
        if (!IsClassBasedHandlerType(type))
        {
            return;
        }

        ImmutableArray<IMethodSymbol> handleMethods = GetDeclaredHandleAsyncMethods(type);
        bool hasTypeRouteAttributes = TelegramHandlerSymbols.HasAnyRouteAttribute(type);
        bool hasHandleRouteAttributes = handleMethods.Any(static method => TelegramHandlerSymbols.HasAnyRouteAttribute(method));
        IMethodSymbol[] routeMethods = GetDeclaredRouteMethodsExceptHandleAsync(type).ToArray();

        if (!hasTypeRouteAttributes &&
            !hasHandleRouteAttributes &&
            routeMethods.Length == 0)
        {
            return;
        }

        Location? location = type.Locations.FirstOrDefault(static candidate => candidate.IsInSource);
        string typeName = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

        foreach (IMethodSymbol routeMethod in routeMethods)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidClassBasedHandler,
                routeMethod.Locations.FirstOrDefault(static candidate => candidate.IsInSource) ?? location,
                $"Class-based Telegram handler type '{typeName}' must declare route metadata on the type or HandleAsync method only."));
        }

        if (type.TypeKind is TypeKind.Interface || type.IsAbstract)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidHandlerType,
                location,
                typeName));
        }

        if (handleMethods.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidClassBasedHandler,
                location,
                $"Class-based Telegram handler type '{typeName}' must declare exactly one public instance HandleAsync method."));
            return;
        }

        IMethodSymbol handleMethod = handleMethods[0];
        HandlerKind? routeKind = GetRouteKind(
            handleMethod,
            includeClassRouteAttributes: true,
            out ITypeSymbol? callbackPayloadType,
            out _,
            out bool hasMixedCallbackRoutes);

        if (!hasMixedCallbackRoutes &&
            routeKind is not null &&
            !IsClassBasedRouteCompatible(type, routeKind.Value, callbackPayloadType, out string reason))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidClassBasedHandler,
                location,
                reason));
        }

        AnalyzeHandlerMethod(context, handleMethod, commands, includeClassRouteAttributes: true);
    }

    private static IEnumerable<IMethodSymbol> GetDeclaredRouteMethodsExceptHandleAsync(INamedTypeSymbol type)
    {
        return type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(static method =>
                method.DeclaredAccessibility == Accessibility.Public &&
                !method.IsStatic &&
                method.MethodKind == MethodKind.Ordinary &&
                !method.IsGenericMethod &&
                !string.Equals(method.Name, "HandleAsync", StringComparison.Ordinal) &&
                TelegramHandlerSymbols.HasAnyRouteAttribute(method));
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

    private static bool IsClassBasedHandlerType(INamedTypeSymbol type)
    {
        return IsAssignableTo(type, TelegramHandlerSymbols.MessageHandler) ||
               IsAssignableTo(type, TelegramHandlerSymbols.CallbackHandler) ||
               IsAssignableTo(type, TelegramHandlerSymbols.ChatMemberUpdateHandler);
    }

    private static bool IsClassBasedRouteCompatible(
        INamedTypeSymbol type,
        HandlerKind kind,
        ITypeSymbol? callbackPayloadType,
        out string reason)
    {
        string typeName = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

        if (IsAssignableTo(type, TelegramHandlerSymbols.MessageHandler))
        {
            if (kind == HandlerKind.Message)
            {
                reason = string.Empty;
                return true;
            }

            reason = $"MessageHandler '{typeName}' can declare only message, text, command, template, or regex routes.";
            return false;
        }

        if (TryGetCallbackHandlerPayloadType(type, out ITypeSymbol handlerPayloadType))
        {
            if (kind == HandlerKind.Callback &&
                callbackPayloadType is not null &&
                SymbolEqualityComparer.Default.Equals(handlerPayloadType, callbackPayloadType))
            {
                reason = string.Empty;
                return true;
            }

            reason = $"CallbackHandler<TPayload> '{typeName}' must use matching CallbackAttribute<TPayload> route metadata.";
            return false;
        }

        if (IsAssignableTo(type, TelegramHandlerSymbols.CallbackHandler))
        {
            if (kind == HandlerKind.Callback &&
                callbackPayloadType is null)
            {
                reason = string.Empty;
                return true;
            }

            reason = $"CallbackHandler '{typeName}' can declare only raw CallbackAttribute route metadata.";
            return false;
        }

        if (IsAssignableTo(type, TelegramHandlerSymbols.ChatMemberUpdateHandler) &&
            kind == HandlerKind.ChatMember)
        {
            reason = string.Empty;
            return true;
        }

        reason = $"ChatMemberUpdateHandler '{typeName}' can declare only chat member update routes.";
        return false;
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
}
