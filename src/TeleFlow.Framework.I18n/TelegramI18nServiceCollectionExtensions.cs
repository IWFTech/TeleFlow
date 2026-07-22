using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TeleFlow.Framework.Middleware;
using TeleFlow.Telegram.I18n.Internal;

namespace TeleFlow.Telegram.I18n;

/// <summary>
/// Registers Telegram update locale resolution and application-provided locale resolvers.
/// </summary>
public static class TelegramI18nServiceCollectionExtensions
{
    /// <summary>
    /// Adds the engine-neutral scoped locale pipeline to the Telegram handler framework.
    /// </summary>
    public static IServiceCollection AddTelegramI18n(
        this IServiceCollection services,
        Action<TelegramI18nOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureTelegramBotRegistered(services, nameof(AddTelegramI18n));

        if (services.Any(static descriptor => descriptor.ServiceType == typeof(TelegramI18nOptions)))
        {
            throw new InvalidOperationException($"{nameof(AddTelegramI18n)} cannot be called more than once.");
        }

        var options = new TelegramI18nOptions();
        configure?.Invoke(options);

        if (options.FallbackLocale is null)
        {
            throw new InvalidOperationException("Telegram i18n fallback locale must be configured.");
        }

        services.AddSingleton(options);
        services.TryAddScoped<LocaleAccessor>();
        services.TryAddScoped<ILocaleAccessor>(static provider => provider.GetRequiredService<LocaleAccessor>());
        services.AddUpdateMiddleware<TelegramLocaleMiddleware>();
        return services;
    }

    /// <summary>
    /// Adds a scoped application locale resolver. Resolvers run in registration order before Telegram language fallback.
    /// </summary>
    public static IServiceCollection AddTelegramLocaleResolver<TResolver>(this IServiceCollection services)
        where TResolver : class, ILocaleResolver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<TResolver>();
        services.AddScoped<ILocaleResolver>(static provider => provider.GetRequiredService<TResolver>());
        return services;
    }

    private static void EnsureTelegramBotRegistered(IServiceCollection services, string apiName)
    {
        if (services.All(static descriptor => descriptor.ServiceType != typeof(TelegramBotOptions)))
        {
            throw new InvalidOperationException(
                $"{nameof(TelegramServiceCollectionExtensions.AddTelegramBot)} must be called before {apiName}.");
        }
    }
}
