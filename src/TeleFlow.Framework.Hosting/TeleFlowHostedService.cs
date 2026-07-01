using Microsoft.Extensions.Hosting;
using TeleFlow.Framework.Application;

namespace TeleFlow.Framework.Hosting;

/// <summary>
/// Bridges the Generic Host background-service lifecycle to a configured TeleFlow application without taking ownership
/// of the host service provider. StartAsync enters the TeleFlow update source, and StopAsync cancels it through the host token.
/// </summary>
internal sealed class TeleFlowHostedService(
    IServiceProvider services,
    TeleFlowHostedServiceCollectionState serviceCollectionState) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var application = TeleFlowApplicationRuntimeFactory.CreateBorrowed(
            services,
            serviceCollectionState.Services);

        await application.RunAsync(stoppingToken).ConfigureAwait(false);
    }
}
