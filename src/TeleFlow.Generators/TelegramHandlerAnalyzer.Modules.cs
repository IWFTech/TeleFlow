using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TeleFlow.Generators;

public sealed partial class TelegramHandlerAnalyzer
{
    private static void AnalyzeTelegramModuleType(SymbolAnalysisContext context, INamedTypeSymbol type)
    {
        if (!TelegramHandlerSymbols.HasAttribute(type, TelegramHandlerSymbols.TelegramModuleAttribute))
        {
            return;
        }

        Location? location = type.Locations.FirstOrDefault(static candidate => candidate.IsInSource);
        string displayName = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

        if (type.TypeKind is TypeKind.Interface || type.IsAbstract || type.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidTelegramModule,
                location,
                $"Telegram module type '{displayName}' must be a concrete class."));
        }

        if (!HasTelegramHandlerMethods(type))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidTelegramModule,
                location,
                $"Telegram module type '{displayName}' must contain at least one Telegram handler method."));
        }
    }

    private static bool HasTelegramHandlerMethods(INamedTypeSymbol type)
    {
        return type.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(static method =>
                method.DeclaredAccessibility == Accessibility.Public &&
                !method.IsStatic &&
                (TelegramHandlerSymbols.HasAnyRouteAttribute(method) ||
                 TelegramHandlerSymbols.HasAnyErrorAttribute(method)));
    }
}
