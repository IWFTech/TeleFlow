using System.Reflection;
using TeleFlow.Annotations;

namespace TeleFlow.Telegram.Internal.Handlers;

internal static class TelegramErrorHandlerDescriptorBuilder
{
    public static IReadOnlyList<TelegramErrorHandlerDescriptor> Build(
        Type handlerType,
        int firstRegistrationOrder)
    {
        ArgumentNullException.ThrowIfNull(handlerType);

        if (handlerType.IsAbstract || handlerType.IsInterface)
        {
            throw new InvalidOperationException(
                $"Telegram error handler type '{handlerType.FullName}' must be a concrete class.");
        }

        var registrationOrder = firstRegistrationOrder;
        var descriptors = new List<TelegramErrorHandlerDescriptor>();
        var moduleName = GetModuleName(handlerType);

        foreach (var method in GetDeclaredCandidateMethods(handlerType))
        {
            var attributes = method.GetCustomAttributes<ErrorAttribute>(inherit: true).ToArray();

            if (attributes.Length == 0)
            {
                continue;
            }

            ValidateReturnType(method);

            foreach (var attribute in attributes)
            {
                var exceptionType = attribute.ExceptionType;

                if (exceptionType is not null && !typeof(Exception).IsAssignableFrom(exceptionType))
                {
                    throw CreateSignatureException(
                        method,
                        $"{nameof(ErrorAttribute)} exception type must derive from {nameof(Exception)}.");
                }

                var parameters = BuildParameterDescriptors(method, exceptionType, out var telegramContextType);

                descriptors.Add(new TelegramErrorHandlerDescriptor(
                    handlerType,
                    method,
                    exceptionType,
                    telegramContextType,
                    registrationOrder,
                    moduleName,
                    parameters));
                registrationOrder++;
            }
        }

        if (HasInheritedErrorMethods(handlerType))
        {
            throw new InvalidOperationException(
                $"Telegram error handler type '{handlerType.FullName}' inherits error handler methods. " +
                "Inherited error handler methods must be overridden in the concrete handler type for generated registration parity.");
        }

        return descriptors;
    }

    private static IEnumerable<MethodInfo> GetDeclaredCandidateMethods(Type handlerType)
    {
        return handlerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .OrderBy(static method => method.MetadataToken);
    }

    private static bool HasInheritedErrorMethods(Type handlerType)
    {
        return handlerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType != handlerType)
            .Any(static method => method.GetCustomAttribute<ErrorAttribute>(inherit: true) is not null);
    }

    private static string? GetModuleName(Type handlerType)
    {
        return handlerType.GetCustomAttribute<TelegramModuleAttribute>(inherit: false)?.Name;
    }

    private static List<TelegramErrorHandlerParameterDescriptor> BuildParameterDescriptors(
        MethodInfo method,
        Type? exceptionType,
        out Type? telegramContextType)
    {
        var parameters = method.GetParameters();

        if (parameters.Count(static parameter => parameter.ParameterType == typeof(TelegramErrorContext)) > 1)
        {
            throw CreateSignatureException(method, $"An error handler method can declare at most one {nameof(TelegramErrorContext)} parameter.");
        }

        if (parameters.Count(static parameter => parameter.ParameterType == typeof(CancellationToken)) > 1)
        {
            throw CreateSignatureException(method, "An error handler method can declare at most one CancellationToken parameter.");
        }

        var exceptionParameters = parameters
            .Where(static parameter => typeof(Exception).IsAssignableFrom(parameter.ParameterType))
            .ToArray();

        if (exceptionParameters.Length > 1)
        {
            throw CreateSignatureException(method, "An error handler method can declare at most one exception parameter.");
        }

        var contextParameters = parameters
            .Where(static parameter => typeof(TelegramUpdateContext).IsAssignableFrom(parameter.ParameterType))
            .ToArray();

        if (contextParameters.Length > 1)
        {
            throw CreateSignatureException(method, "An error handler method can declare at most one Telegram context parameter.");
        }

        telegramContextType = contextParameters.FirstOrDefault()?.ParameterType;

        var descriptors = new List<TelegramErrorHandlerParameterDescriptor>(parameters.Length);

        foreach (var parameter in parameters)
        {
            if (parameter.ParameterType == typeof(TelegramErrorContext))
            {
                descriptors.Add(new TelegramErrorHandlerParameterDescriptor(parameter, TelegramErrorHandlerParameterKind.ErrorContext));
                continue;
            }

            if (parameter.ParameterType == typeof(CancellationToken))
            {
                descriptors.Add(new TelegramErrorHandlerParameterDescriptor(parameter, TelegramErrorHandlerParameterKind.CancellationToken));
                continue;
            }

            if (typeof(Exception).IsAssignableFrom(parameter.ParameterType))
            {
                ValidateExceptionParameter(method, exceptionType, parameter);
                descriptors.Add(new TelegramErrorHandlerParameterDescriptor(parameter, TelegramErrorHandlerParameterKind.Exception));
                continue;
            }

            if (typeof(TelegramUpdateContext).IsAssignableFrom(parameter.ParameterType))
            {
                descriptors.Add(new TelegramErrorHandlerParameterDescriptor(parameter, TelegramErrorHandlerParameterKind.TelegramContext));
                continue;
            }

            if (IsRouteValueParameterType(parameter.ParameterType))
            {
                descriptors.Add(new TelegramErrorHandlerParameterDescriptor(parameter, TelegramErrorHandlerParameterKind.RouteValue));
                continue;
            }

            descriptors.Add(new TelegramErrorHandlerParameterDescriptor(parameter, TelegramErrorHandlerParameterKind.Service));
        }

        return descriptors;
    }

    private static void ValidateExceptionParameter(
        MethodInfo method,
        Type? exceptionType,
        ParameterInfo parameter)
    {
        if (exceptionType is null)
        {
            if (parameter.ParameterType != typeof(Exception))
            {
                throw CreateSignatureException(
                    method,
                    $"Catch-all {nameof(ErrorAttribute)} methods must bind exception parameters as {nameof(Exception)}.");
            }

            return;
        }

        if (!parameter.ParameterType.IsAssignableFrom(exceptionType))
        {
            throw CreateSignatureException(
                method,
                $"Exception parameter '{parameter.Name}' must be assignable from {exceptionType.Name}.");
        }
    }

    private static bool IsRouteValueParameterType(Type type)
    {
        var valueType = Nullable.GetUnderlyingType(type) ?? type;

        return valueType == typeof(string) ||
               valueType == typeof(int) ||
               valueType == typeof(long);
    }

    private static void ValidateReturnType(MethodInfo method)
    {
        if (method.ReturnType == typeof(TelegramErrorHandlingResult) ||
            IsErrorHandlingTaskResult(method.ReturnType, typeof(Task<>)) ||
            IsErrorHandlingTaskResult(method.ReturnType, typeof(ValueTask<>)))
        {
            return;
        }

        throw CreateSignatureException(
            method,
            $"A Telegram error handler method must return {nameof(TelegramErrorHandlingResult)}, Task<{nameof(TelegramErrorHandlingResult)}>, or ValueTask<{nameof(TelegramErrorHandlingResult)}>.");
    }

    private static bool IsErrorHandlingTaskResult(Type returnType, Type taskType)
    {
        return returnType.IsGenericType &&
               returnType.GetGenericTypeDefinition() == taskType &&
               returnType.GetGenericArguments()[0] == typeof(TelegramErrorHandlingResult);
    }

    private static InvalidOperationException CreateSignatureException(MethodInfo method, string reason)
    {
        return new InvalidOperationException(
            $"Invalid Telegram error handler signature '{method.DeclaringType?.FullName}.{method.Name}': {reason}");
    }
}
