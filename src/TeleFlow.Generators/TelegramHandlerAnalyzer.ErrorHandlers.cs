using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TeleFlow.Generators;

public sealed partial class TelegramHandlerAnalyzer
{
    private static void AnalyzeErrorHandlerMethod(
        SymbolAnalysisContext context,
        IMethodSymbol method)
    {
        IReadOnlyList<AttributeData> errorAttributes = TelegramHandlerSymbols.GetErrorAttributes(method, inherit: true);

        if (errorAttributes.Count == 0)
        {
            return;
        }

        Location? location = method.Locations.FirstOrDefault(static candidate => candidate.IsInSource);
        string displayName = method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

        if (method.ContainingType.TypeKind is TypeKind.Interface ||
            method.ContainingType.IsAbstract ||
            method.DeclaredAccessibility != Accessibility.Public ||
            method.IsStatic ||
            method.IsGenericMethod)
        {
            ReportInvalidErrorHandler(
                context,
                location,
                $"Telegram error handler method '{displayName}' must be a public instance method on a concrete class.");
        }

        if (!IsSupportedErrorHandlerReturnType(method.ReturnType))
        {
            ReportInvalidErrorHandler(
                context,
                location,
                $"Telegram error handler method '{displayName}' must return TelegramErrorHandlingResult, Task<TelegramErrorHandlingResult>, or ValueTask<TelegramErrorHandlingResult>.");
        }

        List<ITypeSymbol?> exceptionTypes = GetErrorHandlerExceptionTypes(context, errorAttributes, location);

        ValidateErrorHandlerParameters(
            context,
            method,
            exceptionTypes,
            location);
    }

    private static List<ITypeSymbol?> GetErrorHandlerExceptionTypes(
        SymbolAnalysisContext context,
        IReadOnlyList<AttributeData> attributes,
        Location? location)
    {
        List<ITypeSymbol?> exceptionTypes = new List<ITypeSymbol?>();

        foreach (AttributeData attribute in attributes)
        {
            if (!TelegramHandlerSymbols.IsGenericAttribute(attribute, TelegramHandlerSymbols.GenericErrorAttribute))
            {
                exceptionTypes.Add(null);
                continue;
            }

            if (attribute.AttributeClass is not INamedTypeSymbol { TypeArguments.Length: 1 } genericAttribute ||
                !IsExceptionType(genericAttribute.TypeArguments[0]))
            {
                ReportInvalidErrorHandler(
                    context,
                    location,
                    "ErrorAttribute<TException> generic argument must derive from Exception.");
                continue;
            }

            exceptionTypes.Add(genericAttribute.TypeArguments[0]);
        }

        return exceptionTypes;
    }

    private static void ValidateErrorHandlerParameters(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        IReadOnlyList<ITypeSymbol?> exceptionTypes,
        Location? location)
    {
        if (method.Parameters.Count(static parameter =>
                TelegramHandlerSymbols.IsType(parameter.Type, TelegramHandlerSymbols.TelegramErrorContext)) > 1)
        {
            ReportInvalidErrorHandler(
                context,
                location,
                "A Telegram error handler method can declare at most one TelegramErrorContext parameter.");
        }

        if (method.Parameters.Count(static parameter =>
                TelegramHandlerSymbols.IsType(parameter.Type, TelegramHandlerSymbols.CancellationToken)) > 1)
        {
            ReportInvalidErrorHandler(
                context,
                location,
                "A Telegram error handler method can declare at most one CancellationToken parameter.");
        }

        IParameterSymbol[] exceptionParameters = method.Parameters
            .Where(static parameter => IsExceptionType(parameter.Type))
            .ToArray();

        if (exceptionParameters.Length > 1)
        {
            ReportInvalidErrorHandler(
                context,
                location,
                "A Telegram error handler method can declare at most one exception parameter.");
        }

        IParameterSymbol[] telegramContextParameters = method.Parameters
            .Where(static parameter => IsTelegramContextType(parameter.Type))
            .ToArray();

        if (telegramContextParameters.Length > 1)
        {
            ReportInvalidErrorHandler(
                context,
                location,
                "A Telegram error handler method can declare at most one Telegram context parameter.");
        }

        IParameterSymbol? exceptionParameter = exceptionParameters.FirstOrDefault();

        if (exceptionParameter is null)
        {
            return;
        }

        foreach (ITypeSymbol? exceptionType in exceptionTypes)
        {
            if (exceptionType is null)
            {
                if (!TelegramHandlerSymbols.IsType(exceptionParameter.Type, TelegramHandlerSymbols.Exception))
                {
                    ReportInvalidErrorHandler(
                        context,
                        location,
                        "Catch-all ErrorAttribute methods must bind exception parameters as Exception.");
                }

                continue;
            }

            if (!IsAssignableFrom(exceptionParameter.Type, exceptionType))
            {
                ReportInvalidErrorHandler(
                    context,
                    location,
                    $"Exception parameter '{exceptionParameter.Name}' must be assignable from {exceptionType.Name}.");
            }
        }
    }

    private static bool IsSupportedErrorHandlerReturnType(ITypeSymbol returnType)
    {
        if (TelegramHandlerSymbols.IsType(returnType, TelegramHandlerSymbols.TelegramErrorHandlingResult))
        {
            return true;
        }

        return returnType is INamedTypeSymbol
        {
            IsGenericType: true,
            TypeArguments.Length: 1
        } namedType &&
        TelegramHandlerSymbols.IsType(namedType.TypeArguments[0], TelegramHandlerSymbols.TelegramErrorHandlingResult) &&
        namedType.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) is
            "System.Threading.Tasks.Task<TResult>" or
            "System.Threading.Tasks.ValueTask<TResult>";
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

    private static void ReportInvalidErrorHandler(
        SymbolAnalysisContext context,
        Location? location,
        string message)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            InvalidErrorHandler,
            location,
            message));
    }
}
