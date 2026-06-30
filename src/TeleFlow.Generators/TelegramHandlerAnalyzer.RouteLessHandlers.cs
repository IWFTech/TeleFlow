using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TeleFlow.Generators;

public sealed partial class TelegramHandlerAnalyzer
{
    private static readonly string[] RouteLessHandlerAttributeMetadataNames =
    {
        TelegramHandlerSymbols.StateAttribute,
        TelegramHandlerSymbols.RequireTelegramRoleAttribute,
        TelegramHandlerSymbols.ChatMemberTransitionAttribute,
        TelegramHandlerSymbols.ChatMemberChangedAttribute
    };

    private static readonly string[] RouteLessHandlerGenericAttributeMetadataNames =
    {
        TelegramHandlerSymbols.GenericStateAttribute,
        TelegramHandlerSymbols.GenericUseFilterAttribute
    };

    private static void AnalyzeMissingRouteAttribute(
        SymbolAnalysisContext context,
        IMethodSymbol method)
    {
        if (TelegramHandlerSymbols.HasAnyErrorAttribute(method) ||
            TelegramHandlerSymbols.HasAttribute(method, TelegramHandlerSymbols.SceneStepAttribute, inherit: true) ||
            !TryGetRouteLessHandlerAttribute(method, out AttributeData attribute))
        {
            return;
        }

        string displayName = method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        Location? location = attribute.ApplicationSyntaxReference
            ?.GetSyntax(context.CancellationToken)
            .GetLocation();

        context.ReportDiagnostic(Diagnostic.Create(
            MissingRouteAttribute,
            location ?? method.Locations.FirstOrDefault(static candidate => candidate.IsInSource),
            displayName));
    }

    private static bool TryGetRouteLessHandlerAttribute(
        IMethodSymbol method,
        out AttributeData attribute)
    {
        if (TryGetFirstKnownAttribute(method, RouteLessHandlerAttributeMetadataNames, out attribute) ||
            TryGetFirstKnownGenericAttribute(method, RouteLessHandlerGenericAttributeMetadataNames, out attribute) ||
            TryGetFirst(TelegramBuiltInFilterFacts.GetAttributes(method), out attribute))
        {
            return true;
        }

        attribute = null!;
        return false;
    }

    private static bool TryGetFirstKnownAttribute(
        IMethodSymbol method,
        IReadOnlyList<string> metadataNames,
        out AttributeData attribute)
    {
        foreach (string metadataName in metadataNames)
        {
            if (TryGetFirst(TelegramHandlerSymbols.GetAttributes(method, metadataName, inherit: true), out attribute))
            {
                return true;
            }
        }

        attribute = null!;
        return false;
    }

    private static bool TryGetFirstKnownGenericAttribute(
        IMethodSymbol method,
        IReadOnlyList<string> metadataNames,
        out AttributeData attribute)
    {
        foreach (string metadataName in metadataNames)
        {
            if (TryGetFirst(TelegramHandlerSymbols.GetGenericAttributes(method, metadataName, inherit: true), out attribute))
            {
                return true;
            }
        }

        attribute = null!;
        return false;
    }

    private static bool TryGetFirst(
        IEnumerable<AttributeData> attributes,
        out AttributeData attribute)
    {
        foreach (AttributeData candidate in attributes)
        {
            attribute = candidate;
            return true;
        }

        attribute = null!;
        return false;
    }
}
