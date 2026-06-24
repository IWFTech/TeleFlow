using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TeleFlow.Generators;

public sealed partial class TelegramHandlerAnalyzer
{
    private static void AnalyzeInheritedHandlerMethods(
        SymbolAnalysisContext context,
        INamedTypeSymbol type)
    {
        if (!IsConcreteHandlerCandidate(type))
        {
            return;
        }

        IMethodSymbol[] inheritedHandlerMethods = GetInheritedHandlerMethods(type)
            .Where(method => !IsOverriddenBy(type, method))
            .GroupBy(static method => method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();

        foreach (IMethodSymbol method in inheritedHandlerMethods)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InheritedHandlerMethod,
                type.Locations.FirstOrDefault(static candidate => candidate.IsInSource),
                $"Telegram handler type '{type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}' inherits handler method '{method.Name}'. Inherited handler methods must be overridden in the concrete handler type for generated registration parity."));
        }
    }

    private static IEnumerable<IMethodSymbol> GetInheritedHandlerMethods(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type.BaseType; current is not null; current = current.BaseType)
        {
            foreach (IMethodSymbol method in current.GetMembers().OfType<IMethodSymbol>())
            {
                if (method is not
                    {
                        DeclaredAccessibility: Accessibility.Public,
                        IsStatic: false,
                        IsGenericMethod: false
                    } ||
                    method.MethodKind != MethodKind.Ordinary ||
                    (!TelegramHandlerSymbols.HasAnyRouteAttribute(method) &&
                     !TelegramHandlerSymbols.HasAnyErrorAttribute(method)))
                {
                    continue;
                }

                yield return method;
            }
        }
    }

    private static bool IsConcreteHandlerCandidate(INamedTypeSymbol type)
    {
        if (type.TypeKind is TypeKind.Interface || type.IsAbstract)
        {
            return false;
        }

        if (TelegramHandlerSymbols.HasAttribute(type, TelegramHandlerSymbols.TelegramModuleAttribute))
        {
            return true;
        }

        if (IsClassBasedHandlerType(type) &&
            (TelegramHandlerSymbols.HasAnyRouteAttribute(type) ||
             GetDeclaredHandleAsyncMethods(type).Any(static method => TelegramHandlerSymbols.HasAnyRouteAttribute(method))))
        {
            return true;
        }

        return type.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(static method =>
                method.DeclaredAccessibility == Accessibility.Public &&
                !method.IsStatic &&
                method.MethodKind == MethodKind.Ordinary &&
                !method.IsGenericMethod &&
                (TelegramHandlerSymbols.HasAnyRouteAttribute(method) ||
                 TelegramHandlerSymbols.HasAnyErrorAttribute(method)));
    }

    private static bool IsOverriddenBy(
        INamedTypeSymbol type,
        IMethodSymbol inheritedMethod)
    {
        foreach (IMethodSymbol method in type.GetMembers().OfType<IMethodSymbol>())
        {
            for (IMethodSymbol? current = method.OverriddenMethod; current is not null; current = current.OverriddenMethod)
            {
                if (SymbolEqualityComparer.Default.Equals(
                        current.OriginalDefinition,
                        inheritedMethod.OriginalDefinition))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
