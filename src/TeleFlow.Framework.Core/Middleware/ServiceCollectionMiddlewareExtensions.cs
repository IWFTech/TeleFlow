using Microsoft.Extensions.DependencyInjection;

namespace TeleFlow.Framework.Middleware;

public static class ServiceCollectionMiddlewareExtensions
{
    public static IServiceCollection AddUpdateMiddleware<TMiddleware>(this IServiceCollection services)
        where TMiddleware : class, IUpdateMiddleware
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<TMiddleware>();
        services.AddUpdateMiddlewareRegistration<TMiddleware>();
        return services;
    }

    public static IServiceCollection AddSingletonUpdateMiddleware<TMiddleware>(this IServiceCollection services)
        where TMiddleware : class, IUpdateMiddleware
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TMiddleware>();
        services.AddUpdateMiddlewareRegistration<TMiddleware>();
        return services;
    }

    public static IServiceCollection AddDefaultUpdateRateLimiting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUpdateMiddleware<UpdateRateLimitMiddleware>();
        return services;
    }

    private static void AddUpdateMiddlewareRegistration<TMiddleware>(this IServiceCollection services)
        where TMiddleware : class, IUpdateMiddleware
    {
        var middlewareType = typeof(TMiddleware);

        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(UpdateMiddlewareRegistration) &&
                descriptor.ImplementationInstance is UpdateMiddlewareRegistration registration &&
                registration.MiddlewareType == middlewareType))
        {
            return;
        }

        services.AddSingleton(new UpdateMiddlewareRegistration(middlewareType));
    }
}
