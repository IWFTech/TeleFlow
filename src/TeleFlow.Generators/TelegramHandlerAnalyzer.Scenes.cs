using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TeleFlow.Generators;

public sealed partial class TelegramHandlerAnalyzer
{
    private static void AnalyzeSceneType(SymbolAnalysisContext context, INamedTypeSymbol type)
    {
        AttributeData? sceneAttribute = TelegramHandlerSymbols.GetFirstAttribute(
            type,
            TelegramHandlerSymbols.SceneAttribute);

        if (sceneAttribute is null)
        {
            return;
        }

        Location? location = type.Locations.FirstOrDefault(static candidate => candidate.IsInSource);
        string? prefix = sceneAttribute.ConstructorArguments.Length > 0
            ? sceneAttribute.ConstructorArguments[0].Value as string
            : null;

        if (string.IsNullOrWhiteSpace(prefix))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidScene,
                location,
                $"Scene '{type.Name}' must declare a non-empty prefix."));
        }

        if (type.TypeKind == TypeKind.Interface || type.IsAbstract || type.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidScene,
                location,
                $"Scene '{type.Name}' must be a concrete non-static partial class."));
        }

        foreach (SyntaxReference syntaxReference in type.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax declaration ||
                !declaration.Modifiers.Any(static modifier => modifier.Text == "partial"))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidScene,
                    location,
                    $"Scene '{type.Name}' must be declared as partial."));
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
                InvalidScene,
                location,
                $"Scene '{type.Name}' must declare at least one public static partial State property."));
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
                    InvalidScene,
                    propertyLocation,
                    $"Scene state property '{type.Name}.{property.Name}' must be partial."));
            }
        }

        if (prefix is not null &&
            !string.IsNullOrWhiteSpace(prefix))
        {
            ReportDuplicateSceneStateIds(context, type, prefix, stateProperties, location);
        }
    }

    private static void AnalyzeSceneStep(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        Location? location)
    {
        AttributeData? sceneStep = TelegramHandlerSymbols.GetFirstAttribute(
            method,
            TelegramHandlerSymbols.SceneStepAttribute,
            inherit: true);

        if (sceneStep is null)
        {
            return;
        }

        if (!TelegramHandlerSymbols.HasAttribute(method.ContainingType, TelegramHandlerSymbols.SceneAttribute))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidSceneStep,
                location,
                "SceneStepAttribute can only be used on handler methods declared inside a SceneAttribute type."));
            return;
        }

        if (HasStateBindingAttributes(method.ContainingType) || HasStateBindingAttributes(method))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidSceneStep,
                location,
                "SceneStepAttribute cannot be mixed with StateAttribute or StateAttribute<TStateGroup>."));
            return;
        }

        string? stateName = sceneStep.ConstructorArguments.Length > 0
            ? sceneStep.ConstructorArguments[0].Value as string
            : null;

        if (stateName is null ||
            string.IsNullOrWhiteSpace(stateName) ||
            method.ContainingType.GetMembers(stateName).OfType<IPropertySymbol>().FirstOrDefault(IsUsableStateProperty) is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidSceneStep,
                location,
                $"SceneStepAttribute references missing scene state '{method.ContainingType.Name}.{stateName}'."));
        }
    }

    private static bool HasStateBindingAttributes(ISymbol symbol)
    {
        return TelegramHandlerSymbols.HasAttribute(
                   symbol,
                   TelegramHandlerSymbols.StateAttribute,
                   inherit: true) ||
               TelegramHandlerSymbols.HasGenericAttribute(
                   symbol,
                   TelegramHandlerSymbols.GenericStateAttribute,
                   inherit: true);
    }

    private static void ReportDuplicateSceneStateIds(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        string prefix,
        IReadOnlyList<IPropertySymbol> stateProperties,
        Location? fallbackLocation)
    {
        var duplicate = stateProperties
            .Select(property => new
            {
                Property = property,
                StateId = $"{prefix}:{GetSceneStateSegment(property)}"
            })
            .GroupBy(static item => item.StateId, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicate is null)
        {
            return;
        }

        Location? location = duplicate
            .Select(static item => item.Property.Locations.FirstOrDefault(static candidate => candidate.IsInSource))
            .FirstOrDefault(static candidate => candidate is not null) ?? fallbackLocation;

        context.ReportDiagnostic(Diagnostic.Create(
            InvalidScene,
            location,
            $"Scene '{type.Name}' has duplicate state id '{duplicate.Key}'."));
    }

    private static string GetSceneStateSegment(IPropertySymbol property)
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

        return ToSceneCamelCase(property.Name);
    }

    private static string ToSceneCamelCase(string value)
    {
        return value.Length == 0
            ? value
            : char.ToLowerInvariant(value[0]) + value.Substring(1);
    }
}
