using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TeleFlow.Framework.Middleware;
using TeleFlow.Framework.States;

namespace TeleFlow.Storage.Memory;

public static class MemoryStateServiceCollectionExtensions
{
    public static IServiceCollection AddMemoryStateStorage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IStateStore, MemoryStateStore>();
        services.TryAddSingleton<IStateDataStore, MemoryStateDataStore>();
        services.TryAddSingleton<IStateDataSerializer, JsonStateDataSerializer>();
        services.TryAddSingleton<IStateHistoryStore, MemoryStateHistoryStore>();
        services.TryAddSingleton<IStateStorageKeyBuilder, DefaultStateStorageKeyBuilder>();
        services.AddUpdateMiddleware<UpdateStateMiddleware>();

        return services;
    }
}
