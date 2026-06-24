using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Core.Dispatching;

namespace TeleFlow.Telegram.Internal.Handlers;

internal static class TelegramHandlerServiceRegistrar
{
    public static bool TryRegisterGeneratedHandlerType(
        IServiceCollection services,
        Type handlerType,
        IReadOnlyList<TelegramGeneratedHandlersAttribute> registrarAttributes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentNullException.ThrowIfNull(registrarAttributes);

        EnsureValidModuleType(handlerType);

        var nextOrder = GetNextHandlerRegistrationOrder(services);
        var nextErrorOrder = GetNextErrorHandlerRegistrationOrder(services);
        var newDescriptors = new List<TelegramHandlerDescriptor>();
        var newErrorDescriptors = new List<TelegramErrorHandlerDescriptor>();

        foreach (var attribute in registrarAttributes)
        {
            var registrar = CreateGeneratedRegistrar(attribute.RegistrarType);
            var registry = new TelegramGeneratedHandlerRegistry();

            registrar.Register(registry);

            var descriptors = registry.BuildDescriptors(
                nextOrder,
                descriptor => descriptor.HandlerType == handlerType);
            var errorDescriptors = registry.BuildErrorDescriptors(
                nextErrorOrder,
                descriptor => descriptor.HandlerType == handlerType);

            nextOrder += descriptors.Count;
            nextErrorOrder += errorDescriptors.Count;
            newDescriptors.AddRange(descriptors);
            newErrorDescriptors.AddRange(errorDescriptors);
        }

        if (newDescriptors.Count == 0 && newErrorDescriptors.Count == 0)
        {
            return false;
        }

        TelegramHandlerRegistrationValidator.EnsureHandlerTypesCanRegister(
            services,
            [handlerType],
            TelegramHandlerRegistrationMode.Generated);
        TelegramHandlerRegistrationValidator.EnsureNoDuplicateCommands(services, newDescriptors);
        RegisterHandlerTypesAsNeeded(services, [handlerType]);
        RegisterDescriptors(services, newDescriptors);
        RegisterErrorDescriptors(services, newErrorDescriptors);
        RegisterTypeMarkers(services, [handlerType], TelegramHandlerRegistrationMode.Generated);
        EnsureDefaultDispatcherRegistered(services);

        return true;
    }

    public static void RegisterGeneratedHandlers(
        IServiceCollection services,
        Assembly assembly,
        IReadOnlyList<TelegramGeneratedHandlersAttribute> registrarAttributes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(registrarAttributes);

        TelegramHandlerRegistrationValidator.EnsureAssemblyCanRegister(
            services,
            assembly,
            TelegramHandlerRegistrationMode.Generated);

        var nextOrder = GetNextHandlerRegistrationOrder(services);
        var nextErrorOrder = GetNextErrorHandlerRegistrationOrder(services);
        var newDescriptors = new List<TelegramHandlerDescriptor>();
        var newErrorDescriptors = new List<TelegramErrorHandlerDescriptor>();
        var handlerTypes = new List<Type>();

        foreach (var attribute in registrarAttributes)
        {
            var registrar = CreateGeneratedRegistrar(attribute.RegistrarType);
            var registry = new TelegramGeneratedHandlerRegistry();

            registrar.Register(registry);

            var descriptors = registry.BuildDescriptors(nextOrder);
            var errorDescriptors = registry.BuildErrorDescriptors(nextErrorOrder);

            nextOrder += descriptors.Count;
            nextErrorOrder += errorDescriptors.Count;
            newDescriptors.AddRange(descriptors);
            newErrorDescriptors.AddRange(errorDescriptors);
            handlerTypes.AddRange(registry.HandlerTypes);
        }

        if (newDescriptors.Count == 0 && newErrorDescriptors.Count == 0)
        {
            throw new InvalidOperationException(
                "Generated Telegram handler registrars did not register any handlers.");
        }

        var distinctHandlerTypes = handlerTypes
            .Distinct()
            .ToArray();

        TelegramHandlerRegistrationValidator.EnsureHandlerTypesCanRegister(
            services,
            distinctHandlerTypes,
            TelegramHandlerRegistrationMode.Generated);
        TelegramHandlerRegistrationValidator.EnsureNoDuplicateCommands(services, newDescriptors);
        RegisterHandlerTypesAsNeeded(services, distinctHandlerTypes);
        RegisterDescriptors(services, newDescriptors);
        RegisterErrorDescriptors(services, newErrorDescriptors);
        RegisterAssemblyMarker(services, assembly, TelegramHandlerRegistrationMode.Generated);
        RegisterTypeMarkers(services, distinctHandlerTypes, TelegramHandlerRegistrationMode.Generated);
        EnsureDefaultDispatcherRegistered(services);
    }

    public static void RegisterReflectionHandlers(
        IServiceCollection services,
        Assembly assembly,
        IReadOnlyList<Type> handlerTypes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(handlerTypes);

        TelegramHandlerRegistrationValidator.EnsureAssemblyCanRegister(
            services,
            assembly,
            TelegramHandlerRegistrationMode.Reflection);

        if (handlerTypes.Count == 0)
        {
            throw new InvalidOperationException(
                $"Assembly '{assembly.FullName}' does not contain Telegram handler or error handler types for reflection registration.");
        }

        RegisterHandlerTypes(
            services,
            handlerTypes,
            TelegramHandlerRegistrationMode.Reflection,
            assembly);
    }

    public static void RegisterModuleType(
        IServiceCollection services,
        Type moduleType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(moduleType);

        EnsureValidModuleType(moduleType);
        RegisterHandlerTypes(services, [moduleType]);
    }

    public static void RegisterHandlerTypes(
        IServiceCollection services,
        IReadOnlyList<Type> handlerTypes)
    {
        RegisterHandlerTypes(
            services,
            handlerTypes,
            TelegramHandlerRegistrationMode.Direct,
            assembly: null);
    }

    private static void RegisterHandlerTypes(
        IServiceCollection services,
        IReadOnlyList<Type> handlerTypes,
        TelegramHandlerRegistrationMode registrationMode,
        Assembly? assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(handlerTypes);

        TelegramHandlerRegistrationValidator.EnsureHandlerTypesCanRegister(
            services,
            handlerTypes,
            registrationMode);

        var nextOrder = GetNextHandlerRegistrationOrder(services);
        var nextErrorOrder = GetNextErrorHandlerRegistrationOrder(services);
        var newDescriptors = new List<TelegramHandlerDescriptor>();
        var newErrorDescriptors = new List<TelegramErrorHandlerDescriptor>();

        foreach (var handlerType in handlerTypes)
        {
            var descriptors = TelegramHandlerDescriptorBuilder.Build(handlerType, nextOrder);
            var errorDescriptors = TelegramErrorHandlerDescriptorBuilder.Build(handlerType, nextErrorOrder);

            if (descriptors.Count == 0 && errorDescriptors.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Telegram handler type '{handlerType.FullName}' does not contain Telegram handler or error handler methods.");
            }

            nextOrder += descriptors.Count;
            nextErrorOrder += errorDescriptors.Count;
            newDescriptors.AddRange(descriptors);
            newErrorDescriptors.AddRange(errorDescriptors);
        }

        TelegramHandlerRegistrationValidator.EnsureNoDuplicateCommands(services, newDescriptors);
        RegisterHandlerTypesAsNeeded(services, handlerTypes);
        RegisterDescriptors(services, newDescriptors);
        RegisterErrorDescriptors(services, newErrorDescriptors);
        if (assembly is not null)
        {
            RegisterAssemblyMarker(services, assembly, registrationMode);
        }

        RegisterTypeMarkers(services, handlerTypes, registrationMode);
        EnsureDefaultDispatcherRegistered(services);
    }

    private static void EnsureValidModuleType(Type moduleType)
    {
        if (moduleType.IsAbstract || moduleType.IsInterface)
        {
            throw new InvalidOperationException(
                $"Telegram module type '{moduleType.FullName}' must be a concrete class.");
        }

        if (moduleType.GetCustomAttributes(typeof(TeleFlow.Annotations.TelegramModuleAttribute), inherit: false).Length == 0)
        {
            throw new InvalidOperationException(
                $"Telegram module type '{moduleType.FullName}' must be marked with {nameof(TeleFlow.Annotations.TelegramModuleAttribute)}.");
        }
    }

    private static ITelegramGeneratedHandlerRegistrar CreateGeneratedRegistrar(Type registrarType)
    {
        if (!typeof(ITelegramGeneratedHandlerRegistrar).IsAssignableFrom(registrarType))
        {
            throw new InvalidOperationException(
                $"Generated Telegram handler registrar '{registrarType.FullName}' must implement {nameof(ITelegramGeneratedHandlerRegistrar)}.");
        }

        if (Activator.CreateInstance(registrarType) is not ITelegramGeneratedHandlerRegistrar registrar)
        {
            throw new InvalidOperationException(
                $"Generated Telegram handler registrar '{registrarType.FullName}' could not be created.");
        }

        return registrar;
    }

    private static int GetNextHandlerRegistrationOrder(IServiceCollection services)
    {
        return services
            .Where(static descriptor => descriptor.ServiceType == typeof(TelegramHandlerDescriptor))
            .Select(static descriptor => descriptor.ImplementationInstance)
            .OfType<TelegramHandlerDescriptor>()
            .Select(static descriptor => descriptor.RegistrationOrder)
            .DefaultIfEmpty(-1)
            .Max() + 1;
    }

    private static int GetNextErrorHandlerRegistrationOrder(IServiceCollection services)
    {
        return services
            .Where(static descriptor => descriptor.ServiceType == typeof(TelegramErrorHandlerDescriptor))
            .Select(static descriptor => descriptor.ImplementationInstance)
            .OfType<TelegramErrorHandlerDescriptor>()
            .Select(static descriptor => descriptor.RegistrationOrder)
            .DefaultIfEmpty(-1)
            .Max() + 1;
    }

    private static void RegisterHandlerTypesAsNeeded(
        IServiceCollection services,
        IEnumerable<Type> handlerTypes)
    {
        foreach (var handlerType in handlerTypes.Distinct())
        {
            if (services.All(descriptor => descriptor.ServiceType != handlerType))
            {
                services.AddTransient(handlerType);
            }
        }
    }

    private static void RegisterDescriptors(
        IServiceCollection services,
        IEnumerable<TelegramHandlerDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors)
        {
            services.AddSingleton(descriptor);
        }
    }

    private static void RegisterErrorDescriptors(
        IServiceCollection services,
        IEnumerable<TelegramErrorHandlerDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors)
        {
            services.AddSingleton(descriptor);
        }
    }

    private static void RegisterAssemblyMarker(
        IServiceCollection services,
        Assembly assembly,
        TelegramHandlerRegistrationMode mode)
    {
        services.AddSingleton(new TelegramHandlerAssemblyRegistrationMarker(assembly, mode));
    }

    private static void RegisterTypeMarkers(
        IServiceCollection services,
        IEnumerable<Type> handlerTypes,
        TelegramHandlerRegistrationMode mode)
    {
        foreach (var handlerType in handlerTypes.Distinct())
        {
            services.AddSingleton(new TelegramHandlerTypeRegistrationMarker(handlerType, mode));
        }
    }

    private static void EnsureDefaultDispatcherRegistered(IServiceCollection services)
    {
        if (services.All(static descriptor => descriptor.ServiceType != typeof(IUpdateDispatcher)))
        {
            services.AddSingleton<IUpdateDispatcher, TelegramHandlerDispatcher>();
        }
    }
}
