using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace TeleFlow.Framework.Hosting;

/// <summary>
/// Adds the Microsoft.Extensions.Hosting adapter that runs a configured TeleFlow application through the Generic Host lifecycle.
/// Use this with long-running update sources such as long polling; ASP.NET Core webhooks are driven by endpoint routing instead.
/// </summary>
public static class TeleFlowHostingServiceCollectionExtensions
{
    public static IServiceCollection AddTeleFlowHostedService(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new TeleFlowHostedServiceCollectionState(services));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TeleFlowHostedService>());

        return services;
    }
}
