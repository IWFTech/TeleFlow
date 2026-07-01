using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TeleFlow.Framework.States;
using TeleFlow.Framework.Updates;

namespace TeleFlow.Framework.Application;

internal sealed class TeleFlowApplicationBuilder : ITeleFlowApplicationBuilder
{
    public TeleFlowApplicationBuilder(string[]? args)
    {
        Services = new ServiceCollection();
        Services.AddSingleton(new TeleFlowApplicationArguments(args));
        Services.TryAddSingleton<IStateStorageKeyBuilder, DefaultStateStorageKeyBuilder>();
        Services.AddUpdateContextAccessor();
    }

    public IServiceCollection Services { get; }

    public ITeleFlowApplication Build()
    {
        return TeleFlowApplicationRuntimeFactory.CreateOwned(Services);
    }
}
