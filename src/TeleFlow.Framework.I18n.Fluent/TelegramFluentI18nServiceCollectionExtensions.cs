using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TeleFlow.Framework.Application;
using TeleFlow.Telegram.I18n.Fluent.Internal;

namespace TeleFlow.Telegram.I18n.Fluent;

/// <summary>
/// Registers startup-loaded Project Fluent catalogs, scoped update localization, and explicit-locale formatting.
/// </summary>
public static class TelegramFluentI18nServiceCollectionExtensions
{
    /// <summary>
    /// Adds Telegram locale resolution and the Linguini-backed Fluent adapter as optional framework services.
    /// </summary>
    public static IServiceCollection AddTelegramFluentI18n(
        this IServiceCollection services,
        Action<TelegramFluentI18nOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(static descriptor => descriptor.ServiceType == typeof(TelegramFluentI18nOptions)))
        {
            throw new InvalidOperationException($"{nameof(AddTelegramFluentI18n)} cannot be called more than once.");
        }

        var options = new TelegramFluentI18nOptions();
        configure?.Invoke(options);

        if (string.IsNullOrWhiteSpace(options.ResourcesPath))
        {
            throw new InvalidOperationException("Fluent resource path must not be empty.");
        }

        if (options.FallbackLocale is null)
        {
            throw new InvalidOperationException("Fluent fallback locale must be configured.");
        }

        services.AddTelegramI18n(i18n => i18n.FallbackLocale = options.FallbackLocale);
        services.AddSingleton(options);
        services.AddSingleton<FluentCatalog>();
        services.AddSingleton<IFluentTextFormatter, FluentTextFormatter>();
        services.AddScoped<IFluentLocalizer, FluentLocalizer>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ITeleFlowRuntimeValidator, FluentCatalogRuntimeValidator>());
        return services;
    }
}
