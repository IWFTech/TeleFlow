using Microsoft.Extensions.DependencyInjection;

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
        return TeleFlowApplicationRuntimeFactory.CreateOwned(Services);
    }
}
