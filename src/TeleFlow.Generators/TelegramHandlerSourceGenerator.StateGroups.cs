using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TeleFlow.Generators;

public sealed partial class TelegramHandlerSourceGenerator
{
    private static GeneratedStateGroup? GetStateGroup(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol type ||
            !TryGetStateContainerPrefix(type, out string prefix))
        {
            return null;
        }

        ImmutableArray<GeneratedStateProperty> states = type
            .GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static property => IsUsableStateProperty(property) && IsPartialStateProperty(property))
            .Select(static property => new GeneratedStateProperty(
                property.Name,
                GetStateSegment(property)))
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ToImmutableArray();

        if (states.Length == 0)
        {
            return null;
        }

        TypeDeclarationSyntax syntax = (TypeDeclarationSyntax)context.Node;

        return new GeneratedStateGroup(
            TypeName: type.Name,
            TypeMetadataName: type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            Namespace: type.ContainingNamespace?.IsGlobalNamespace == false
                ? type.ContainingNamespace.ToDisplayString()
                : null,
            Accessibility: GetAccessibility(type.DeclaredAccessibility),
            IsStatic: type.IsStatic,
            Prefix: prefix,
            States: states,
            SourceSpanStart: syntax.SpanStart);
    }

    private static string GetAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            _ => "internal"
        };
    }
}
