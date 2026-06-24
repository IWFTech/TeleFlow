using Microsoft.Extensions.DependencyInjection;

namespace TeleFlow.Core.Application;

public interface ITeleFlowApplicationBuilder
{
    IServiceCollection Services { get; }

    ITeleFlowApplication Build();
}
