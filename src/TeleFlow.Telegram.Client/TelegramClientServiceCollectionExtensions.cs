using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Internal.Options;

namespace TeleFlow.Telegram;

public static class TelegramClientServiceCollectionExtensions
{
    public static IServiceCollection AddTelegramClient(
        this IServiceCollection services,
        Action<TelegramClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        EnsureNotRegistered<TelegramClientOptions>(services, nameof(AddTelegramClient));

        var options = new TelegramClientOptions();
        configure(options);
        TelegramClientOptionsValidator.Validate(options);

        services.AddSingleton(options);
        AddTelegramClientDefaults(services);

        return services;
    }

    public static IServiceCollection AddDeepLinkPayloadSerializer<TSerializer>(
        this IServiceCollection services)
        where TSerializer : class, IDeepLinkPayloadSerializer
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IDeepLinkPayloadSerializer>();
        services.AddSingleton<IDeepLinkPayloadSerializer, TSerializer>();
        return services;
    }

    public static IServiceCollection AddTelegramJsonOptions(
        this IServiceCollection services,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.RemoveAll<TelegramJsonOptions>();
        services.AddSingleton(new TelegramJsonOptions(options));
        return services;
    }

    public static IServiceCollection AddTelegramJsonOptions(
        this IServiceCollection services,
        Action<JsonSerializerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = TelegramJsonOptions.CreateDefault().SerializerOptions;
        configure(options);

        services.RemoveAll<TelegramJsonOptions>();
        services.AddSingleton(new TelegramJsonOptions(options));
        return services;
    }

    public static IServiceCollection AddTelegramHttpTransport(
        this IServiceCollection services,
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(httpClient);

        services.RemoveAll<ITelegramTransport>();
        services.AddSingleton<ITelegramTransport>(_ => new HttpClientTelegramTransport(httpClient));
        return services;
    }

    public static IServiceCollection AddTelegramHttpTransport(
        this IServiceCollection services,
        Func<IServiceProvider, HttpClient> httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        services.RemoveAll<ITelegramTransport>();
        services.AddSingleton<ITelegramTransport>(provider =>
        {
            var httpClient = httpClientFactory(provider);
            ArgumentNullException.ThrowIfNull(httpClient);
            return HttpClientTelegramTransport.CreateOwned(httpClient);
        });
        return services;
    }

    public static IServiceCollection AddTelegramTransport<TTransport>(
        this IServiceCollection services)
        where TTransport : class, ITelegramTransport
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<ITelegramTransport>();
        services.AddSingleton<ITelegramTransport, TTransport>();
        return services;
    }

    public static IServiceCollection AddTelegramHttpMessageHandler<THandler>(
        this IServiceCollection services)
        where THandler : HttpMessageHandler
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<ITelegramTransport>();
        services.AddSingleton<ITelegramTransport>(provider =>
            HttpClientTelegramTransport.CreateOwned(new HttpClient(
                provider.GetRequiredService<THandler>(),
                disposeHandler: false)));
        return services;
    }

    public static IServiceCollection AddTelegramClient<TClient>(
        this IServiceCollection services)
        where TClient : class, ITelegramClient
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<ITelegramClient>();
        services.AddSingleton<ITelegramClient, TClient>();
        return services;
    }

    public static IServiceCollection AddTelegramRequestExecutor<TExecutor>(
        this IServiceCollection services)
        where TExecutor : class, ITelegramRequestExecutor
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<ITelegramRequestExecutor>();
        services.AddSingleton<ITelegramRequestExecutor, TExecutor>();
        return services;
    }

    private static void AddTelegramClientDefaults(IServiceCollection services)
    {
        services.TryAddSingleton(TelegramJsonOptions.CreateDefault());
        services.TryAddSingleton<IDeepLinkPayloadSerializer, Base64UrlJsonDeepLinkPayloadSerializer>();
        services.TryAddSingleton<TelegramDeepLinks>(static provider =>
            new TelegramDeepLinks(
                provider.GetRequiredService<TelegramClientOptions>().BotUsername,
                provider.GetRequiredService<IDeepLinkPayloadSerializer>()));
        services.TryAddSingleton<ITelegramTransport>(static _ => HttpClientTelegramTransport.CreateOwned(new HttpClient()));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.TryAddSingleton<ITelegramClient, TelegramClient>();
        services.TryAddSingleton<ITelegramRequestExecutor, TelegramRequestExecutor>();
    }

    private static void EnsureNotRegistered<TService>(IServiceCollection services, string apiName)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(TService)))
        {
            throw new InvalidOperationException(
                $"{apiName} cannot be called more than once for service '{typeof(TService).Name}'.");
        }
    }
}
