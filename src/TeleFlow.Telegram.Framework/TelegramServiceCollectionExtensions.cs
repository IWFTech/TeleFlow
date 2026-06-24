using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TeleFlow.Core.Callbacks;
using TeleFlow.Core.Dispatching;
using TeleFlow.Core.States;
using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Internal.Handlers;
using TeleFlow.Telegram.Internal.Options;

namespace TeleFlow.Telegram;

public static class TelegramServiceCollectionExtensions
{
    public static IServiceCollection AddTelegramBot(
        this IServiceCollection services,
        Action<TelegramBotOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        EnsureNotRegistered<TelegramBotOptions>(services, nameof(AddTelegramBot));

        var options = new TelegramBotOptions();
        configure(options);
        TelegramBotOptionsValidator.Validate(options);

        services.AddTelegramClient(clientOptions =>
        {
            clientOptions.Token = options.Token;
            clientOptions.BotUsername = options.BotUsername;
            clientOptions.BaseUrl = options.BaseUrl;
            clientOptions.Defaults = options.Defaults;
        });

        services.AddSingleton(options);
        services.TryAddSingleton(options.RoleFilter);
        services.AddSingleton<TelegramContextFactory>();
        services.TryAddSingleton<IStateKeyFactory, TelegramStateKeyFactory>();
        services.TryAddSingleton<ICallbackDataSerializer, JsonCallbackDataSerializer>();
        services.TryAddSingleton<ITelegramChatMemberStatusResolver, TelegramChatMemberStatusResolver>();
        services.TryAddSingleton<ITelegramChatMemberStatusCache, MemoryTelegramChatMemberStatusCache>();

        return services;
    }

    public static IServiceCollection AddTelegramChatMemberStatusResolver<TResolver>(
        this IServiceCollection services)
        where TResolver : class, ITelegramChatMemberStatusResolver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<ITelegramChatMemberStatusResolver>();
        services.AddSingleton<ITelegramChatMemberStatusResolver, TResolver>();
        return services;
    }

    public static IServiceCollection AddTelegramChatMemberStatusCache<TCache>(
        this IServiceCollection services)
        where TCache : class, ITelegramChatMemberStatusCache
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<ITelegramChatMemberStatusCache>();
        services.AddSingleton<ITelegramChatMemberStatusCache, TCache>();
        return services;
    }

    public static IServiceCollection AddAutoCallbackAnswer(
        this IServiceCollection services,
        Action<TelegramAutoAnswerCallbackOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureTelegramBotRegistered(services, nameof(AddAutoCallbackAnswer));

        var options = new TelegramAutoAnswerCallbackOptions();
        configure?.Invoke(options);
        ValidateAutoAnswerCallbackOptions(options);

        services.RemoveAll<TelegramAutoAnswerCallbackOptions>();
        services.AddSingleton(options);
        return services;
    }

    public static IServiceCollection AddTelegramHandlersFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        EnsureTelegramBotRegistered(services, nameof(AddTelegramHandlersFromAssembly));

        var generatedRegistrars = TelegramHandlerAssemblyScanner.GetGeneratedRegistrars(assembly);

        if (generatedRegistrars.Length > 0)
        {
            TelegramHandlerServiceRegistrar.RegisterGeneratedHandlers(services, assembly, generatedRegistrars);
            return services;
        }

        throw new InvalidOperationException(
            $"Assembly '{assembly.FullName}' does not contain generated Telegram handler metadata. " +
            $"Use {nameof(AddTelegramHandler)}<THandler>() or {nameof(AddTelegramModule)}<TModule>() for explicit direct registration.");
    }

    public static IServiceCollection AddTelegramHandlersFromAssemblyReflection(
        this IServiceCollection services,
        Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        EnsureTelegramBotRegistered(services, nameof(AddTelegramHandlersFromAssemblyReflection));
        TelegramHandlerRegistrationValidator.EnsureAssemblyCanRegister(
            services,
            assembly,
            TelegramHandlerRegistrationMode.Reflection);

        var handlerTypes = TelegramHandlerAssemblyScanner.GetHandlerTypes(assembly);

        TelegramHandlerServiceRegistrar.RegisterReflectionHandlers(services, assembly, handlerTypes);
        return services;
    }

    public static IServiceCollection AddTelegramHandler<THandler>(this IServiceCollection services)
        where THandler : class
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureTelegramBotRegistered(services, nameof(AddTelegramHandler));
        TelegramHandlerServiceRegistrar.RegisterHandlerTypes(services, [typeof(THandler)]);

        return services;
    }

    public static IServiceCollection AddTelegramModule<TModule>(this IServiceCollection services)
        where TModule : class
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureTelegramBotRegistered(services, nameof(AddTelegramModule));

        var moduleType = typeof(TModule);
        var generatedRegistrars = TelegramHandlerAssemblyScanner.GetGeneratedRegistrars(moduleType.Assembly);

        if (generatedRegistrars.Length > 0 &&
            TelegramHandlerServiceRegistrar.TryRegisterGeneratedHandlerType(services, moduleType, generatedRegistrars))
        {
            return services;
        }

        TelegramHandlerServiceRegistrar.RegisterModuleType(services, moduleType);

        return services;
    }

    private static void EnsureTelegramBotRegistered(IServiceCollection services, string apiName)
    {
        if (services.All(static descriptor => descriptor.ServiceType != typeof(TelegramBotOptions)))
        {
            throw new InvalidOperationException(
                $"{nameof(AddTelegramBot)} must be called before {apiName}.");
        }
    }

    private static void EnsureNotRegistered<TService>(IServiceCollection services, string apiName)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(TService)))
        {
            throw new InvalidOperationException(
                $"{apiName} cannot be called more than once for service '{typeof(TService).Name}'.");
        }
    }

    private static void ValidateAutoAnswerCallbackOptions(TelegramAutoAnswerCallbackOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Text is not null && string.IsNullOrWhiteSpace(options.Text))
        {
            throw new InvalidOperationException("Auto callback answer text must not be empty.");
        }

        options.Text = options.Text?.Trim();
    }

}
