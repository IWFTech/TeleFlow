using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace TeleFlow.Telegram.Internal.Handlers;

internal static class TelegramErrorHandlerInvoker
{
    public static async ValueTask<TelegramErrorHandlingResult> InvokeAsync(
        TelegramErrorHandlerDescriptor handler,
        TelegramErrorContext errorContext,
        TelegramUpdateContext telegramContext,
        Exception exception,
        IReadOnlyDictionary<string, object?> routeValues,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(errorContext);
        ArgumentNullException.ThrowIfNull(telegramContext);
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(routeValues);

        var arguments = new object?[handler.Parameters.Count];

        for (var index = 0; index < handler.Parameters.Count; index++)
        {
            var parameter = handler.Parameters[index];

            arguments[index] = parameter.Kind switch
            {
                TelegramErrorHandlerParameterKind.ErrorContext => errorContext,
                TelegramErrorHandlerParameterKind.TelegramContext => GetTelegramContext(handler, parameter, telegramContext),
                TelegramErrorHandlerParameterKind.Exception => GetException(handler, parameter, exception),
                TelegramErrorHandlerParameterKind.RouteValue => GetRouteValue(handler, parameter, routeValues),
                TelegramErrorHandlerParameterKind.Service => telegramContext.Services.GetRequiredService(parameter.ParameterType),
                TelegramErrorHandlerParameterKind.CancellationToken => cancellationToken,
                _ => throw new InvalidOperationException($"Unsupported error handler parameter kind '{parameter.Kind}'.")
            };
        }

        if (handler.GeneratedInvoker is not null)
        {
            return ValidateResult(
                handler,
                await handler.GeneratedInvoker(telegramContext.Services, arguments, cancellationToken).ConfigureAwait(false));
        }

        if (handler.Method is null)
        {
            throw new InvalidOperationException(
                $"Telegram error handler '{GetDisplayName(handler)}' has no invoker.");
        }

        var handlerInstance = telegramContext.Services.GetRequiredService(handler.HandlerType);
        object? result;

        try
        {
            result = handler.Method.Invoke(handlerInstance, arguments);
        }
        catch (TargetInvocationException reflectionException) when (reflectionException.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(reflectionException.InnerException).Throw();
            throw;
        }

        return result switch
        {
            TelegramErrorHandlingResult handlingResult => ValidateResult(handler, handlingResult),
            Task<TelegramErrorHandlingResult> task => ValidateResult(handler, await task.ConfigureAwait(false)),
            ValueTask<TelegramErrorHandlingResult> valueTask => ValidateResult(handler, await valueTask.ConfigureAwait(false)),
            _ => throw new InvalidOperationException(
                $"Telegram error handler '{GetDisplayName(handler)}' returned an unsupported result.")
        };
    }

    private static TelegramUpdateContext GetTelegramContext(
        TelegramErrorHandlerDescriptor handler,
        TelegramErrorHandlerParameterDescriptor parameter,
        TelegramUpdateContext context)
    {
        if (!parameter.ParameterType.IsInstanceOfType(context))
        {
            throw new InvalidOperationException(
                $"Telegram error handler '{GetDisplayName(handler)}' requires {parameter.ParameterType.Name}, but the selected update context is {context.GetType().Name}.");
        }

        return context;
    }

    private static Exception GetException(
        TelegramErrorHandlerDescriptor handler,
        TelegramErrorHandlerParameterDescriptor parameter,
        Exception exception)
    {
        if (!parameter.ParameterType.IsInstanceOfType(exception))
        {
            throw new InvalidOperationException(
                $"Telegram error handler '{GetDisplayName(handler)}' requires {parameter.ParameterType.Name}, but the thrown exception is {exception.GetType().Name}.");
        }

        return exception;
    }

    private static object? GetRouteValue(
        TelegramErrorHandlerDescriptor handler,
        TelegramErrorHandlerParameterDescriptor parameter,
        IReadOnlyDictionary<string, object?> routeValues)
    {
        if (string.IsNullOrWhiteSpace(parameter.Name))
        {
            throw new InvalidOperationException(
                $"Telegram error handler '{GetDisplayName(handler)}' has an unnamed route value parameter.");
        }

        if (!routeValues.TryGetValue(parameter.Name, out var value))
        {
            throw new InvalidOperationException(
                $"Telegram error handler '{GetDisplayName(handler)}' requires route value '{parameter.Name}', but the failed handler route did not provide it.");
        }

        if (value is null)
        {
            if (parameter.ParameterType.IsValueType && Nullable.GetUnderlyingType(parameter.ParameterType) is null)
            {
                throw new InvalidOperationException(
                    $"Telegram error handler '{GetDisplayName(handler)}' route value '{parameter.Name}' is null and cannot be assigned to {parameter.ParameterType.Name}.");
            }

            return null;
        }

        var underlyingParameterType = Nullable.GetUnderlyingType(parameter.ParameterType);

        if (underlyingParameterType is not null &&
            underlyingParameterType.IsInstanceOfType(value))
        {
            return value;
        }

        if (!parameter.ParameterType.IsInstanceOfType(value))
        {
            throw new InvalidOperationException(
                $"Telegram error handler '{GetDisplayName(handler)}' route value '{parameter.Name}' has type {value.GetType().Name}, but {parameter.ParameterType.Name} was expected.");
        }

        return value;
    }

    private static TelegramErrorHandlingResult ValidateResult(
        TelegramErrorHandlerDescriptor handler,
        TelegramErrorHandlingResult result)
    {
        return result switch
        {
            TelegramErrorHandlingResult.Handled => result,
            TelegramErrorHandlingResult.Unhandled => result,
            _ => throw new InvalidOperationException(
                $"Telegram error handler '{GetDisplayName(handler)}' returned unsupported error handling result '{result}'.")
        };
    }

    private static string GetDisplayName(TelegramErrorHandlerDescriptor handler)
    {
        return $"{handler.HandlerType.FullName}.{handler.MethodName}";
    }
}
