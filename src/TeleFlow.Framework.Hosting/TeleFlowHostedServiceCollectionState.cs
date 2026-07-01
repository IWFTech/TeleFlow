using Microsoft.Extensions.DependencyInjection;

namespace TeleFlow.Framework.Hosting;

/// <summary>
/// Keeps the host service collection available until the hosted service starts, so Core can apply the same
/// configuration validation that the manual TeleFlow application builder applies before creating the runtime pipeline.
/// </summary>
internal sealed class TeleFlowHostedServiceCollectionState
{
    public TeleFlowHostedServiceCollectionState(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IServiceCollection Services { get; }
}
