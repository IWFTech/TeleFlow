using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TeleFlow.Generators;

public sealed partial class TelegramHandlerAnalyzer
{
    private static void AnalyzeStateGroupType(SymbolAnalysisContext context, INamedTypeSymbol type)
    {
        if (!TelegramHandlerSymbols.HasAttribute(type, TelegramHandlerSymbols.StateGroupAttribute))
        {
            return;
        }

        Location? location = type.Locations.FirstOrDefault(static candidate => candidate.IsInSource);

        if (type.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidStateGroup,
                location,
                $"State group '{type.Name}' must be a non-static partial class so it can be used as a generic StateAttribute argument."));
        }

        foreach (SyntaxReference syntaxReference in type.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax declaration ||
                !declaration.Modifiers.Any(static modifier => modifier.Text == "partial"))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidStateGroup,
                    location,
                    $"State group '{type.Name}' must be declared as partial."));
                break;
            }
        }

        IPropertySymbol[] stateProperties = type
            .GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static property => property.DeclaredAccessibility == Accessibility.Public &&
                                      property.IsStatic &&
                                      TelegramHandlerSymbols.IsType(property.Type, TelegramHandlerSymbols.State))
            .ToArray();

        if (stateProperties.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidStateGroup,
                location,
                $"State group '{type.Name}' must declare at least one public static partial State property."));
        }

        foreach (IPropertySymbol property in stateProperties)
        {
            Location? propertyLocation = property.Locations.FirstOrDefault(static candidate => candidate.IsInSource) ?? location;
            bool isPartial = property
                .DeclaringSyntaxReferences
                .Select(static reference => reference.GetSyntax())
                .OfType<PropertyDeclarationSyntax>()
                .Any(static declaration => declaration.Modifiers.Any(modifier => modifier.Text == "partial"));

            if (!isPartial)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidStateGroup,
                    propertyLocation,
                    $"State group property '{type.Name}.{property.Name}' must be partial."));
            }
        }
    }

    private static void ValidateTypedStateAttributes(SymbolAnalysisContext context, ISymbol symbol)
    {
        foreach (AttributeData attribute in TelegramHandlerSymbols.GetGenericAttributes(
                     symbol,
                     TelegramHandlerSymbols.GenericStateAttribute,
                     inherit: true))
        {
            Location? location = symbol.Locations.FirstOrDefault(static candidate => candidate.IsInSource);

            if (attribute.AttributeClass is not INamedTypeSymbol { TypeArguments.Length: 1 } attributeType ||
                attributeType.TypeArguments[0] is not INamedTypeSymbol groupType ||
                !HasStateContainerAttribute(groupType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidTypedState,
                    location,
                    "Typed StateAttribute must reference a type marked with StateGroupAttribute or SceneAttribute."));
                continue;
            }

            string? stateName = attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value as string
                : null;

            if (stateName is null ||
                string.IsNullOrWhiteSpace(stateName) ||
                groupType.GetMembers(stateName).OfType<IPropertySymbol>().FirstOrDefault(IsUsableStateProperty) is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidTypedState,
                    location,
                    $"Typed StateAttribute references missing state '{groupType.Name}.{stateName}'."));
            }
        }
    }

    private static bool HasStateContainerAttribute(INamedTypeSymbol type)
    {
        return TelegramHandlerSymbols.HasAttribute(type, TelegramHandlerSymbols.StateGroupAttribute) ||
               TelegramHandlerSymbols.HasAttribute(type, TelegramHandlerSymbols.SceneAttribute);
    }

    private static bool IsUsableStateProperty(IPropertySymbol property)
    {
        return property.DeclaredAccessibility == Accessibility.Public &&
               property.IsStatic &&
               TelegramHandlerSymbols.IsType(property.Type, TelegramHandlerSymbols.State);
    }
}
