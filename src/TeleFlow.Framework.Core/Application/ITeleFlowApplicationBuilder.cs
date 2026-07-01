using Microsoft.Extensions.DependencyInjection;

namespace TeleFlow.Framework.Application;

public interface ITeleFlowApplicationBuilder
{
    IServiceCollection Services { get; }

    ITeleFlowApplication Build();
}
