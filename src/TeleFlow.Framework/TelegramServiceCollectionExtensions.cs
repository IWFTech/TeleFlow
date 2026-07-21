using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TeleFlow.Framework.Application;
using TeleFlow.Framework.Callbacks;
using TeleFlow.Framework.Dispatching;
using TeleFlow.Framework.States;
using TeleFlow.Framework.Updates;
using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Internal.Handlers;
using TeleFlow.Telegram.Internal.Options;

namespace TeleFlow.Telegram;

/// <summary>
/// Registers Telegram bot framework services, handler metadata, and Telegram-specific extension points.
/// </summary>
public static class TelegramServiceCollectionExtensions
{
    private const string ReflectionAssemblyRegistrationObsoleteDiagnosticId = "TLF900";

    private const string ReflectionAssemblyRegistrationObsoleteMessage =
        "Reflection-based assembly handler registration is deprecated and will be removed before 1.0. " +
        "Use AddTelegramHandlersFromAssembly with IWF.TeleFlow.Generators, or register handlers explicitly with AddTelegramHandler<THandler> / AddTelegramModule<TModule>.";

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
        var stateKeyOptions = TelegramStateKeyOptions.FromToken(options.Token);

        services.AddTelegramClient(clientOptions =>
        {
            clientOptions.Token = options.Token;
            clientOptions.BotUsername = options.BotUsername;
            clientOptions.BaseUrl = options.BaseUrl;
            clientOptions.Environment = options.Environment;
            clientOptions.Defaults = options.Defaults;
            clientOptions.RetryAfter = options.RetryAfter;
        });

        services.AddSingleton(options);
        services.AddSingleton(stateKeyOptions);
        services.AddUpdateContextAccessor();
        services.TryAddSingleton(options.RoleFilter);
        services.AddSingleton<TelegramContextFactory>();
        services.TryAddScoped<ITelegramCurrentUpdateAccessor, TelegramCurrentUpdateAccessor>();
        services.TryAddSingleton<IStateStorageKeyBuilder, DefaultStateStorageKeyBuilder>();
        services.TryAddSingleton<IStateKeyFactory, TelegramStateKeyFactory>();
        services.TryAddSingleton<ICallbackDataSerializer, JsonCallbackDataSerializer>();
        services.TryAddSingleton<ITelegramChatMemberStatusResolver, TelegramChatMemberStatusResolver>();
        services.TryAddSingleton<ITelegramChatMemberStatusCache, MemoryTelegramChatMemberStatusCache>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITeleFlowRuntimeValidator, TelegramRuntimeDependencyValidator>());

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

    /// <summary>
    /// Registers all generated Telegram handler metadata from an assembly.
    /// This API requires build-time metadata from <c>IWF.TeleFlow.Generators</c> and never falls back to assembly scanning.
    /// </summary>
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

    /// <summary>
    /// Registers all Telegram handler types discovered by scanning an assembly at startup.
    /// This API is a deprecated migration path and should not be used by new applications.
    /// </summary>
    [Obsolete(
        ReflectionAssemblyRegistrationObsoleteMessage,
        DiagnosticId = ReflectionAssemblyRegistrationObsoleteDiagnosticId)]
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

    /// <summary>
    /// Registers one explicitly named handler type by building its metadata at startup without scanning its assembly.
    /// </summary>
    public static IServiceCollection AddTelegramHandler<THandler>(this IServiceCollection services)
        where THandler : class
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureTelegramBotRegistered(services, nameof(AddTelegramHandler));
        TelegramHandlerServiceRegistrar.RegisterHandlerTypes(services, [typeof(THandler)]);

        return services;
    }

    /// <summary>
    /// Registers one explicitly named Telegram module type. Generated metadata is used when available; otherwise
    /// metadata is built at startup for the named module type only.
    /// </summary>
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
