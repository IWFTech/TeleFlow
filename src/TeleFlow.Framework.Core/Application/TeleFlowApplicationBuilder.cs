using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Framework.Updates;

namespace TeleFlow.Framework.Application;

internal sealed class TeleFlowApplicationBuilder : ITeleFlowApplicationBuilder
{
    public TeleFlowApplicationBuilder(string[]? args)
    {
        Services = new ServiceCollection();
        Services.AddSingleton(new TeleFlowApplicationArguments(args));
        Services.AddUpdateContextAccessor();
    }

    public IServiceCollection Services { get; }

    public ITeleFlowApplication Build()
    {
        return TeleFlowApplicationRuntimeFactory.CreateOwned(Services);
    }
}
