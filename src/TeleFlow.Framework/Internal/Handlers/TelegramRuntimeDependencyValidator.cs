using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Framework.Application;

namespace TeleFlow.Telegram.Internal.Handlers;

/// <summary>
/// Validates Telegram handler metadata against the final DI service provider so missing method-parameter
/// services and custom filters fail before an update reaches the dispatcher hot path.
/// </summary>
internal sealed class TelegramRuntimeDependencyValidator : ITeleFlowRuntimeValidator
{
    public void Validate(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var serviceAvailability = services.GetService<IServiceProviderIsService>();

        if (serviceAvailability is null)
        {
            throw new TeleFlowConfigurationException(
                "TeleFlow runtime validation requires a Microsoft.Extensions.DependencyInjection service provider.");
        }

        var errors = new List<string>();

        ValidateHandlers(services, serviceAvailability, errors);
        ValidateErrorHandlers(services, serviceAvailability, errors);

        if (errors.Count == 0)
        {
            return;
        }

        throw new TeleFlowConfigurationException(
            "TeleFlow configuration is invalid." +
            Environment.NewLine +
            Environment.NewLine +
            string.Join(Environment.NewLine + Environment.NewLine, errors));
    }

    private static void ValidateHandlers(
        IServiceProvider services,
        IServiceProviderIsService serviceAvailability,
        List<string> errors)
    {
        foreach (var handler in services.GetServices<TelegramHandlerDescriptor>())
        {
            foreach (var parameter in handler.Parameters.Where(static parameter =>
                         parameter.Kind == TelegramHandlerParameterKind.Service))
            {
                if (!serviceAvailability.IsService(parameter.ParameterType))
                {
                    errors.Add(
                        "Handler dependency was not registered." +
                        Environment.NewLine +
                        $"Handler: {TelegramHandlerDescriptorFormatter.GetDisplayName(handler)}" +
                        Environment.NewLine +
                        $"Parameter: {parameter.Name ?? "<unnamed>"}" +
                        Environment.NewLine +
                        $"Service type: {FormatType(parameter.ParameterType)}" +
                        Environment.NewLine +
                        $"Register {FormatType(parameter.ParameterType)} in IServiceCollection before building the TeleFlow application.");
                }
            }

            foreach (var filterType in GetCustomFilterTypes(handler))
            {
                if (!serviceAvailability.IsService(filterType))
                {
                    errors.Add(
                        "Custom filter was not registered." +
                        Environment.NewLine +
                        $"Handler: {TelegramHandlerDescriptorFormatter.GetDisplayName(handler)}" +
                        Environment.NewLine +
                        $"Filter type: {FormatType(filterType)}" +
                        Environment.NewLine +
                        $"Register {FormatType(filterType)} in IServiceCollection before building the TeleFlow application.");
                }
            }
        }
    }

    private static void ValidateErrorHandlers(
        IServiceProvider services,
        IServiceProviderIsService serviceAvailability,
        List<string> errors)
    {
        foreach (var handler in services.GetServices<TelegramErrorHandlerDescriptor>())
        {
            foreach (var parameter in handler.Parameters.Where(static parameter =>
                         parameter.Kind == TelegramErrorHandlerParameterKind.Service))
            {
                if (!serviceAvailability.IsService(parameter.ParameterType))
                {
                    errors.Add(
                        "Error handler dependency was not registered." +
                        Environment.NewLine +
                        $"Error handler: {FormatErrorHandler(handler)}" +
                        Environment.NewLine +
                        $"Parameter: {parameter.Name ?? "<unnamed>"}" +
                        Environment.NewLine +
                        $"Service type: {FormatType(parameter.ParameterType)}" +
                        Environment.NewLine +
                        $"Register {FormatType(parameter.ParameterType)} in IServiceCollection before building the TeleFlow application.");
                }
            }
        }
    }

    private static IEnumerable<Type> GetCustomFilterTypes(TelegramHandlerDescriptor handler)
    {
        return handler.Route.Filters
            .Select(static filter => filter.CustomFilterType)
            .OfType<Type>()
            .Distinct();
    }

    private static string FormatErrorHandler(TelegramErrorHandlerDescriptor handler)
    {
        return $"{handler.HandlerType.FullName}.{handler.MethodName}";
    }

    private static string FormatType(Type type)
    {
        return type.FullName ?? type.Name;
    }
}
