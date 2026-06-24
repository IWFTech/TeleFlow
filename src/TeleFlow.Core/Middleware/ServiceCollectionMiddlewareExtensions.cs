using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TeleFlow.Core.RateLimiting;

namespace TeleFlow.Core.Middleware;

public static class ServiceCollectionMiddlewareExtensions
{
    public static IServiceCollection AddUpdateMiddleware<TMiddleware>(this IServiceCollection services)
        where TMiddleware : class, IUpdateMiddleware
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IUpdateMiddleware, TMiddleware>();
        return services;
    }

    public static IServiceCollection AddDefaultUpdateRateLimiting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IUpdateMiddleware, UpdateRateLimitMiddleware>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IUpdateRateLimiter, NoOpUpdateRateLimiter>());
        return services;
    }
}
