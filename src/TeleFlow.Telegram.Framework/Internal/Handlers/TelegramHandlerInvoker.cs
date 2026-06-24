using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace TeleFlow.Telegram.Internal.Handlers;

internal static class TelegramHandlerInvoker
{
    public static async Task InvokeAsync(
        TelegramRouteSelection selection,
        TelegramUpdateContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var handler = selection.Handler;
        var arguments = new object?[handler.Parameters.Count];

        for (var index = 0; index < handler.Parameters.Count; index++)
        {
            var parameter = handler.Parameters[index];

            arguments[index] = parameter.Kind switch
            {
                TelegramHandlerParameterKind.Context => context,
                TelegramHandlerParameterKind.CallbackPayload => selection.CallbackPayload,
                TelegramHandlerParameterKind.RouteValue => GetRouteValue(selection, parameter),
                TelegramHandlerParameterKind.Service => context.Services.GetRequiredService(parameter.ParameterType),
                TelegramHandlerParameterKind.CancellationToken => cancellationToken,
                _ => throw new InvalidOperationException($"Unsupported handler parameter kind '{parameter.Kind}'.")
            };
        }

        if (handler.GeneratedInvoker is not null)
        {
            await handler.GeneratedInvoker(context.Services, arguments, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (handler.Method is null)
        {
            throw new InvalidOperationException(
                $"Telegram handler '{TelegramHandlerDescriptorFormatter.GetDisplayName(handler)}' has no invoker.");
        }

        var handlerInstance = context.Services.GetRequiredService(handler.HandlerType);
        object? result;

        try
        {
            result = handler.Method.Invoke(handlerInstance, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }

        switch (result)
        {
            case Task task:
                await task.ConfigureAwait(false);
                break;
            case ValueTask valueTask:
                await valueTask.ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException(
                    $"Telegram handler '{TelegramHandlerDescriptorFormatter.GetDisplayName(handler)}' returned an unsupported result.");
        }
    }

    private static object? GetRouteValue(
        TelegramRouteSelection selection,
        TelegramHandlerParameterDescriptor parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter.Name))
        {
            throw new InvalidOperationException(
                $"Telegram handler '{TelegramHandlerDescriptorFormatter.GetDisplayName(selection.Handler)}' has an unnamed route value parameter.");
        }

        if (!selection.RouteValues.TryGetValue(parameter.Name, out var value))
        {
            throw new InvalidOperationException(
                $"Telegram handler '{TelegramHandlerDescriptorFormatter.GetDisplayName(selection.Handler)}' requires route value '{parameter.Name}', but the selected route did not provide it.");
        }

        if (value is null)
        {
            if (parameter.ParameterType.IsValueType && Nullable.GetUnderlyingType(parameter.ParameterType) is null)
            {
                throw new InvalidOperationException(
                    $"Telegram handler '{TelegramHandlerDescriptorFormatter.GetDisplayName(selection.Handler)}' route value '{parameter.Name}' is null and cannot be assigned to {parameter.ParameterType.Name}.");
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
                $"Telegram handler '{TelegramHandlerDescriptorFormatter.GetDisplayName(selection.Handler)}' route value '{parameter.Name}' has type {value.GetType().Name}, but {parameter.ParameterType.Name} was expected.");
        }

        return value;
    }
}
