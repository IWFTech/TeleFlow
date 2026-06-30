using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Core.DependencyInjection;
using TeleFlow.Core.Dispatching;
using TeleFlow.Core.Middleware;
using TeleFlow.Core.Updates;

namespace TeleFlow.Core.Application;

/// <summary>
/// Composes the Core runtime pipeline from registered services: update source, update processor,
/// dispatcher, middleware, and application lifecycle tasks.
/// </summary>
internal static class TeleFlowApplicationRuntimeFactory
{
    public static ITeleFlowApplication CreateOwned(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        ValidateServiceDescriptors(services);

        var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        try
        {
            return Create(serviceProvider, ownsServices: true);
        }
        catch
        {
            serviceProvider.Dispose();
            throw;
        }
    }

    public static ITeleFlowApplication CreateBorrowed(
        IServiceProvider serviceProvider,
        IEnumerable<ServiceDescriptor>? serviceDescriptors = null)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (serviceDescriptors is not null)
        {
            ValidateServiceDescriptors(serviceDescriptors);
        }

        return Create(serviceProvider, ownsServices: false);
    }

    private static ITeleFlowApplication Create(IServiceProvider serviceProvider, bool ownsServices)
    {
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var updateSource = ResolveSingleRequiredService<IUpdateSource>(serviceProvider);
        var dispatcher = ResolveSingleRequiredService<IUpdateDispatcher>(serviceProvider);
        var middleware = serviceProvider.GetServices<UpdateMiddlewareRegistration>().ToArray();
        var startupTasks = serviceProvider.GetServices<TeleFlowStartupTaskRegistration>().ToArray();
        var shutdownTasks = serviceProvider.GetServices<TeleFlowShutdownTaskRegistration>().ToArray();
        var processor = new DefaultUpdateProcessor(scopeFactory, dispatcher, middleware);
        var lifecycle = new TeleFlowApplicationLifecycleRunner(scopeFactory, startupTasks, shutdownTasks);

        return new TeleFlowApplication(serviceProvider, updateSource, processor, lifecycle, ownsServices);
    }

    private static TService ResolveSingleRequiredService<TService>(IServiceProvider serviceProvider)
        where TService : class
    {
        var services = serviceProvider.GetServices<TService>().ToArray();

        return services.Length switch
        {
            0 => throw new InvalidOperationException(
                $"A single {typeof(TService).Name} registration is required to build the TeleFlow application."),
            > 1 => throw new InvalidOperationException(
                $"Only one {typeof(TService).Name} registration is supported when building the TeleFlow application."),
            _ => services[0]
        };
    }

    private static void ValidateServiceDescriptors(IEnumerable<ServiceDescriptor> services)
    {
        var descriptors = services as ICollection<ServiceDescriptor> ?? services.ToArray();

        if (descriptors.Any(static descriptor => descriptor.ServiceType == typeof(IUpdateMiddleware)))
        {
            throw new InvalidOperationException(
                "Direct IUpdateMiddleware service registrations are not used by the TeleFlow update pipeline. " +
                $"Register middleware with {nameof(ServiceCollectionMiddlewareExtensions.AddUpdateMiddleware)}<TMiddleware>() " +
                $"or {nameof(ServiceCollectionMiddlewareExtensions.AddSingletonUpdateMiddleware)}<TMiddleware>() so TeleFlow can " +
                "resolve it from the correct update scope.");
        }

        if (descriptors.Any(static descriptor => descriptor.ServiceType == typeof(ITeleFlowStartupTask)))
        {
            throw new InvalidOperationException(
                "Direct ITeleFlowStartupTask service registrations are not used by the TeleFlow application lifecycle. " +
                $"Register startup tasks with {nameof(ApplicationLifecycleServiceCollectionExtensions.AddTeleFlowStartupTask)}<TTask>() " +
                "so TeleFlow can resolve them from a dedicated lifecycle scope.");
        }

        if (descriptors.Any(static descriptor => descriptor.ServiceType == typeof(ITeleFlowShutdownTask)))
        {
            throw new InvalidOperationException(
                "Direct ITeleFlowShutdownTask service registrations are not used by the TeleFlow application lifecycle. " +
                $"Register shutdown tasks with {nameof(ApplicationLifecycleServiceCollectionExtensions.AddTeleFlowShutdownTask)}<TTask>() " +
                "so TeleFlow can resolve them from a dedicated lifecycle scope.");
        }
    }
}
