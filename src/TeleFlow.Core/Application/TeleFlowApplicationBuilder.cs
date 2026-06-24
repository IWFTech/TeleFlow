using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Core.Dispatching;
using TeleFlow.Core.Middleware;
using TeleFlow.Core.Updates;

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
        var serviceProvider = Services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var updateSource = ResolveSingleRequiredService<IUpdateSource>(serviceProvider);
        var dispatcher = ResolveSingleRequiredService<IUpdateDispatcher>(serviceProvider);
        var middleware = serviceProvider.GetServices<IUpdateMiddleware>().ToArray();
        var processor = new DefaultUpdateProcessor(scopeFactory, dispatcher, middleware);

        return new TeleFlowApplication(serviceProvider, updateSource, processor);
    }

    private static TService ResolveSingleRequiredService<TService>(IServiceProvider serviceProvider)
        where TService : class
    {
        var services = serviceProvider.GetServices<TService>().ToArray();

        return services.Length switch
        {
            0 => throw new InvalidOperationException(
                $"A single {typeof(TService).Name} registration is required to build the TeleFlow application."),
            > 1 => throw new InvalidOperationException(
                $"Only one {typeof(TService).Name} registration is supported when building the TeleFlow application."),
            _ => services[0]
        };
    }
}
