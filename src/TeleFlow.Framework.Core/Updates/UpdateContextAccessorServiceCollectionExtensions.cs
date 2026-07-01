using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TeleFlow.Framework.Updates;

/// <summary>
/// Registers the scoped current-update accessor used by the TeleFlow runtime pipeline.
/// Framework entry packages call this automatically; custom Core-only applications can call it explicitly.
/// </summary>
public static class UpdateContextAccessorServiceCollectionExtensions
{
    /// <summary>
    /// Registers scoped services that expose the current update during TeleFlow update processing.
    /// </summary>
    public static IServiceCollection AddUpdateContextAccessor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<DefaultUpdateContextAccessor>();
        services.TryAddScoped<IUpdateContextAccessor>(
            static provider => provider.GetRequiredService<DefaultUpdateContextAccessor>());
        services.TryAddScoped<IUpdateContextAccessorInitializer>(
            static provider => provider.GetRequiredService<DefaultUpdateContextAccessor>());

        return services;
    }
}
