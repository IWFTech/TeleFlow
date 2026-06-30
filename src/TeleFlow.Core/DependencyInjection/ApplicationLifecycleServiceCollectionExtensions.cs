using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Core.Application;

namespace TeleFlow.Core.DependencyInjection;

/// <summary>
/// Registers TeleFlow application lifecycle tasks that execute before and after update source processing.
/// These registration APIs store task types so the runtime can create each task from the correct lifecycle scope.
/// </summary>
public static class ApplicationLifecycleServiceCollectionExtensions
{
    /// <summary>
    /// Registers a startup task that runs before the configured update source starts processing updates.
    /// </summary>
    public static IServiceCollection AddTeleFlowStartupTask<TTask>(this IServiceCollection services)
        where TTask : class, ITeleFlowStartupTask
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new TeleFlowStartupTaskRegistration(typeof(TTask)));
        return services;
    }

    /// <summary>
    /// Registers a shutdown task that runs after the configured update source stops processing updates.
    /// </summary>
    public static IServiceCollection AddTeleFlowShutdownTask<TTask>(this IServiceCollection services)
        where TTask : class, ITeleFlowShutdownTask
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new TeleFlowShutdownTaskRegistration(typeof(TTask)));
        return services;
    }
}
