using Microsoft.Extensions.DependencyInjection;

namespace TeleFlow.Framework.Application;

/// <summary>
/// Runs framework runtime validators from application and transport entrypoints before update processing starts.
/// This keeps validation centralized while individual validators remain owned by their framework layer.
/// </summary>
internal static class TeleFlowRuntimeValidatorRunner
{
    public static void Validate(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        List<Exception>? exceptions = null;

        foreach (var validator in services.GetServices<ITeleFlowRuntimeValidator>())
        {
            try
            {
                validator.Validate(services);
            }
            catch (TeleFlowConfigurationException exception)
            {
                exceptions ??= [];
                exceptions.Add(exception);
            }
        }

        if (exceptions is null)
        {
            return;
        }

        if (exceptions.Count == 1)
        {
            throw exceptions[0];
        }

        throw new TeleFlowConfigurationException(
            "TeleFlow configuration is invalid." +
            Environment.NewLine +
            Environment.NewLine +
            string.Join(
                Environment.NewLine + Environment.NewLine,
                exceptions.Select(static exception => exception.Message)),
            new AggregateException(exceptions));
    }
}
