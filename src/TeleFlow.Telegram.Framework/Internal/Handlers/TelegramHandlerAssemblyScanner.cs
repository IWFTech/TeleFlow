using System.Reflection;
using TeleFlow.Annotations;

namespace TeleFlow.Telegram.Internal.Handlers;

internal static class TelegramHandlerAssemblyScanner
{
    public static TelegramGeneratedHandlersAttribute[] GetGeneratedRegistrars(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly
            .GetCustomAttributes<TelegramGeneratedHandlersAttribute>()
            .OrderBy(static attribute => attribute.RegistrarType.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    public static Type[] GetHandlerTypes(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return GetAssemblyTypes(assembly)
            .Where(static type => type is { IsAbstract: false, IsInterface: false })
            .Where(HasTelegramHandlerMethods)
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    private static Type[] GetAssemblyTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            var loaderMessages = exception.LoaderExceptions
                .Where(static loaderException => loaderException is not null)
                .Select(static loaderException => loaderException!.Message)
                .Where(static message => !string.IsNullOrWhiteSpace(message))
                .Distinct(StringComparer.Ordinal)
                .Take(5)
                .ToArray();
            var details = loaderMessages.Length == 0
                ? string.Empty
                : $" Loader exceptions: {string.Join(" | ", loaderMessages)}";

            throw new InvalidOperationException(
                $"Could not load Telegram handler types from assembly '{assembly.FullName}'.{details}",
                exception);
        }
    }

    private static bool HasTelegramHandlerMethods(Type handlerType)
    {
        if (IsClassBasedHandlerType(handlerType) &&
            HasDeclaredHandleAsync(handlerType) &&
            HasRouteAttributes(handlerType))
        {
            return true;
        }

        return handlerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Any(static method => !method.IsSpecialName &&
                                  (HasRouteAttributes(method) || HasErrorAttributes(method)));
    }

    private static bool IsClassBasedHandlerType(Type handlerType)
    {
        return typeof(MessageHandler).IsAssignableFrom(handlerType) ||
               typeof(CallbackHandler).IsAssignableFrom(handlerType) ||
               typeof(ChatMemberUpdateHandler).IsAssignableFrom(handlerType);
    }

    private static bool HasDeclaredHandleAsync(Type handlerType)
    {
        return handlerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Any(static method => !method.IsSpecialName &&
                                  string.Equals(method.Name, "HandleAsync", StringComparison.Ordinal));
    }

    private static bool HasRouteAttributes(MemberInfo member)
    {
        return member.GetCustomAttributes<CommandAttribute>(inherit: true).Any() ||
               member.GetCustomAttributes<MessageAttribute>(inherit: true).Any() ||
               member.GetCustomAttributes<TextAttribute>(inherit: true).Any() ||
               member.GetCustomAttributes<TextTemplateAttribute>(inherit: true).Any() ||
               member.GetCustomAttributes<CommandTemplateAttribute>(inherit: true).Any() ||
               member.GetCustomAttributes<TextRegexAttribute>(inherit: true).Any() ||
               member.GetCustomAttributes<CommandRegexAttribute>(inherit: true).Any() ||
               member.GetCustomAttributes<CallbackAttribute>(inherit: true).Any() ||
               HasGenericCallbackAttribute(member) ||
               member.GetCustomAttributes<ChatMemberUpdatedAttribute>(inherit: true).Any() ||
               member.GetCustomAttributes<MyChatMemberUpdatedAttribute>(inherit: true).Any();
    }

    private static bool HasErrorAttributes(MemberInfo member)
    {
        return member.GetCustomAttributes<ErrorAttribute>(inherit: true).Any();
    }

    private static bool HasGenericCallbackAttribute(MemberInfo member)
    {
        return member
            .GetCustomAttributes(inherit: true)
            .Select(static attribute => attribute.GetType())
            .Any(static type =>
                type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(CallbackAttribute<>));
    }
}
