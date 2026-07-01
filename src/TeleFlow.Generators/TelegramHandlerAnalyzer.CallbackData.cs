using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TeleFlow.Generators;

public sealed partial class TelegramHandlerAnalyzer
{
    private static void AnalyzeCallbackDataType(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        ConcurrentBag<CallbackPrefixRegistration> callbackPrefixes)
    {
        AttributeData? attribute = TelegramHandlerSymbols.GetFirstAttribute(
            type,
            TelegramHandlerSymbols.CallbackDataAttribute);

        if (attribute is null)
        {
            return;
        }

        Location? location = type.Locations.FirstOrDefault(static candidate => candidate.IsInSource);
        string? prefix = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string
            : null;

        if (!TelegramCallbackDataFacts.IsValidPayloadPrefix(prefix))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidCallbackData,
                location,
                $"Callback data payload type '{type.Name}' must declare a non-empty prefix without ':', '%', or whitespace and at most {TelegramCallbackDataFacts.MaxCallbackDataBytes} UTF-8 bytes."));
            return;
        }

        callbackPrefixes.Add(new CallbackPrefixRegistration(prefix!, location));

        foreach (IPropertySymbol field in GetCallbackDataFields(type))
        {
            if (!TelegramCallbackDataFacts.IsSupportedFieldType(field.Type))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidCallbackData,
                    field.Locations.FirstOrDefault(static candidate => candidate.IsInSource) ?? location,
                    $"Callback data field '{type.Name}.{field.Name}' must be string, int, long, bool, or enum."));
            }
        }
    }

    private static IEnumerable<IPropertySymbol> GetCallbackDataFields(INamedTypeSymbol type)
    {
        IMethodSymbol? constructor = type
            .Constructors
            .Where(static candidate => !candidate.IsStatic && candidate.DeclaredAccessibility == Accessibility.Public)
            .Where(static candidate => candidate.Parameters.Length > 0)
            .OrderByDescending(static candidate => candidate.Parameters.Length)
            .FirstOrDefault();

        if (constructor is not null)
        {
            foreach (IParameterSymbol parameter in constructor.Parameters)
            {
                IPropertySymbol? property = type
                    .GetMembers()
                    .OfType<IPropertySymbol>()
                    .FirstOrDefault(property => string.Equals(
                        property.Name,
                        parameter.Name,
                        StringComparison.OrdinalIgnoreCase));

                if (property is not null)
                {
                    yield return property;
                }
            }

            yield break;
        }

        foreach (IPropertySymbol property in type
                     .GetMembers()
                     .OfType<IPropertySymbol>()
                     .Where(static property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic))
        {
            yield return property;
        }
    }

}
