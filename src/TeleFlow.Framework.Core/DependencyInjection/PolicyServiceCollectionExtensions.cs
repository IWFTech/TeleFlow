using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TeleFlow.Framework.Callbacks;
using TeleFlow.Framework.Dispatching;
using TeleFlow.Framework.RateLimiting;
using TeleFlow.Framework.States;
using TeleFlow.Framework.Updates;

namespace TeleFlow.Framework.DependencyInjection;

public static class PolicyServiceCollectionExtensions
{
    public static IServiceCollection AddUpdateDispatcher<TDispatcher>(this IServiceCollection services)
        where TDispatcher : class, IUpdateDispatcher
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IUpdateDispatcher>();
        services.AddSingleton<IUpdateDispatcher, TDispatcher>();
        return services;
    }

    public static IServiceCollection AddUpdateSource<TSource>(this IServiceCollection services)
        where TSource : class, IUpdateSource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IUpdateSource>();
        services.AddSingleton<IUpdateSource, TSource>();
        return services;
    }

    public static IServiceCollection AddCallbackDataSerializer<TSerializer>(this IServiceCollection services)
        where TSerializer : class, ICallbackDataSerializer
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<ICallbackDataSerializer>();
        services.AddSingleton<ICallbackDataSerializer, TSerializer>();
        return services;
    }

    public static IServiceCollection AddStateStore<TStore>(this IServiceCollection services)
        where TStore : class, IStateStore
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IStateStore>();
        services.AddSingleton<IStateStore, TStore>();
        return services;
    }

    public static IServiceCollection AddStateDataStore<TStore>(this IServiceCollection services)
        where TStore : class, IStateDataStore
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IStateDataStore>();
        services.AddSingleton<IStateDataStore, TStore>();
        return services;
    }

    public static IServiceCollection AddStateDataSerializer<TSerializer>(this IServiceCollection services)
        where TSerializer : class, IStateDataSerializer
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IStateDataSerializer>();
        services.AddSingleton<IStateDataSerializer, TSerializer>();
        return services;
    }

    public static IServiceCollection AddStateHistoryStore<TStore>(this IServiceCollection services)
        where TStore : class, IStateHistoryStore
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IStateHistoryStore>();
        services.AddSingleton<IStateHistoryStore, TStore>();
        return services;
    }

    public static IServiceCollection AddStateKeyFactory<TFactory>(this IServiceCollection services)
        where TFactory : class, IStateKeyFactory
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IStateKeyFactory>();
        services.AddSingleton<IStateKeyFactory, TFactory>();
        return services;
    }

    public static IServiceCollection AddStateStorageKeyBuilder<TBuilder>(this IServiceCollection services)
        where TBuilder : class, IStateStorageKeyBuilder
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IStateStorageKeyBuilder>();
        services.AddSingleton<IStateStorageKeyBuilder, TBuilder>();
        return services;
    }

    public static IServiceCollection AddUpdateRateLimiter<TLimiter>(this IServiceCollection services)
        where TLimiter : class, IUpdateRateLimiter
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IUpdateRateLimiter, TLimiter>());
        return services;
    }
}
