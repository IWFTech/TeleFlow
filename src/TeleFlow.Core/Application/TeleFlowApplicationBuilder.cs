using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Core.DependencyInjection;
using TeleFlow.Core.Dispatching;
using TeleFlow.Core.Middleware;
using TeleFlow.Core.Updates;

namespace TeleFlow.Core.Application;

internal sealed class TeleFlowApplicationBuilder : ITeleFlowApplicationBuilder
{
    public TeleFlowApplicationBuilder(string[]? args)
    {
        Services = new ServiceCollection();
        Services.AddSingleton(new TeleFlowApplicationArguments(args));
    }

    public IServiceCollection Services { get; }

    public ITeleFlowApplication Build()
    {
        ValidateMiddlewareRegistrations();
        ValidateLifecycleTaskRegistrations();

        var serviceProvider = Services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var updateSource = ResolveSingleRequiredService<IUpdateSource>(serviceProvider);
        var dispatcher = ResolveSingleRequiredService<IUpdateDispatcher>(serviceProvider);
        var middleware = serviceProvider.GetServices<UpdateMiddlewareRegistration>().ToArray();
        var startupTasks = serviceProvider.GetServices<TeleFlowStartupTaskRegistration>().ToArray();
        var shutdownTasks = serviceProvider.GetServices<TeleFlowShutdownTaskRegistration>().ToArray();
        var processor = new DefaultUpdateProcessor(scopeFactory, dispatcher, middleware);
        var lifecycle = new TeleFlowApplicationLifecycleRunner(scopeFactory, startupTasks, shutdownTasks);

        return new TeleFlowApplication(serviceProvider, updateSource, processor, lifecycle);
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

    private void ValidateMiddlewareRegistrations()
    {
        if (!Services.Any(static descriptor => descriptor.ServiceType == typeof(IUpdateMiddleware)))
        {
            return;
        }

        throw new InvalidOperationException(
            "Direct IUpdateMiddleware service registrations are not used by the TeleFlow update pipeline. " +
            $"Register middleware with {nameof(ServiceCollectionMiddlewareExtensions.AddUpdateMiddleware)}<TMiddleware>() " +
            $"or {nameof(ServiceCollectionMiddlewareExtensions.AddSingletonUpdateMiddleware)}<TMiddleware>() so TeleFlow can " +
            "resolve it from the correct update scope.");
    }

    private void ValidateLifecycleTaskRegistrations()
    {
        if (Services.Any(static descriptor => descriptor.ServiceType == typeof(ITeleFlowStartupTask)))
        {
            throw new InvalidOperationException(
                "Direct ITeleFlowStartupTask service registrations are not used by the TeleFlow application lifecycle. " +
                $"Register startup tasks with {nameof(ApplicationLifecycleServiceCollectionExtensions.AddTeleFlowStartupTask)}<TTask>() " +
                "so TeleFlow can resolve them from a dedicated lifecycle scope.");
        }

        if (Services.Any(static descriptor => descriptor.ServiceType == typeof(ITeleFlowShutdownTask)))
        {
            throw new InvalidOperationException(
                "Direct ITeleFlowShutdownTask service registrations are not used by the TeleFlow application lifecycle. " +
                $"Register shutdown tasks with {nameof(ApplicationLifecycleServiceCollectionExtensions.AddTeleFlowShutdownTask)}<TTask>() " +
                "so TeleFlow can resolve them from a dedicated lifecycle scope.");
        }
    }
}
