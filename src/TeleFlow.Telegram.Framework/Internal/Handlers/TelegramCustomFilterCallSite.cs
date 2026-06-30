using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace TeleFlow.Telegram.Internal.Handlers;

/// <summary>
/// Stores a validated custom filter invocation path prepared while the handler table is built.
/// Incoming updates reuse this call site so custom filter evaluation does not discover interfaces,
/// inspect attributes, or create reflection calls on the dispatcher hot path.
/// </summary>
internal sealed class TelegramCustomFilterCallSite
{
    private static readonly MethodInfo UntypedInvokerMethod = typeof(TelegramCustomFilterCallSite)
        .GetMethod(nameof(InvokeUntypedFilterAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo TypedInvokerMethod = typeof(TelegramCustomFilterCallSite)
        .GetMethod(nameof(InvokeTypedFilterAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly TelegramCustomFilterInvoker _invoker;

    private TelegramCustomFilterCallSite(
        Type filterType,
        Attribute? attribute,
        TelegramCustomFilterInvoker invoker)
    {
        FilterType = filterType;
        Attribute = attribute;
        _invoker = invoker;
    }

    public Type FilterType { get; }

    public Attribute? Attribute { get; }

    public static TelegramCustomFilterCallSite Create(
        Type filterType,
        Type? contextType,
        Attribute? attribute,
        TelegramHandlerKind handlerKind)
    {
        ValidateConcreteFilterType(filterType);

        var expectedContextType = GetExpectedContextType(handlerKind);

        if (attribute is null)
        {
            var resolvedContextType = contextType ?? ResolveUntypedFilterContextType(filterType, expectedContextType);

            ValidateResolvedContextType(filterType, resolvedContextType, expectedContextType);

            var invoker = CreateUntypedInvoker(resolvedContextType);

            return new TelegramCustomFilterCallSite(filterType, attribute: null, invoker);
        }

        var attributeType = attribute.GetType();

        if (attributeType.IsGenericType)
        {
            throw new InvalidOperationException(
                $"Custom Telegram filter attribute type '{attributeType.FullName}' must be non-generic.");
        }

        var typedContextType = contextType ?? ResolveTypedFilterContextType(filterType, expectedContextType, attributeType);

        ValidateResolvedContextType(filterType, typedContextType, expectedContextType);

        var typedInvoker = CreateTypedInvoker(typedContextType, attributeType);

        return new TelegramCustomFilterCallSite(filterType, attribute, typedInvoker);
    }

    public ValueTask<bool> MatchesAsync(
        TelegramUpdateContext context,
        CancellationToken cancellationToken)
    {
        var filter = context.Services.GetRequiredService(FilterType);
        return _invoker(filter, context, Attribute, cancellationToken);
    }

    private static Type ResolveUntypedFilterContextType(
        Type filterType,
        Type expectedContextType)
    {
        var contextType = ResolveContextType(
            filterType,
            expectedContextType,
            static type => type.IsGenericType &&
                           type.GetGenericTypeDefinition() == typeof(ITelegramFilter<>),
            static type => type.GetGenericArguments()[0]);

        if (contextType is null)
        {
            throw new InvalidOperationException(
                $"Custom Telegram filter type '{filterType.FullName}' must implement ITelegramFilter<TContext> compatible with {expectedContextType.Name}.");
        }

        return contextType;
    }

    private static Type ResolveTypedFilterContextType(
        Type filterType,
        Type expectedContextType,
        Type attributeType)
    {
        var contextType = ResolveContextType(
            filterType,
            expectedContextType,
            type => type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(ITelegramFilter<,>) &&
                    type.GetGenericArguments()[1] == attributeType,
            static type => type.GetGenericArguments()[0]);

        if (contextType is null)
        {
            throw new InvalidOperationException(
                $"Parameterized custom Telegram filter type '{filterType.FullName}' must implement ITelegramFilter<TContext, {attributeType.Name}> compatible with {expectedContextType.Name}.");
        }

        return contextType;
    }

    private static Type? ResolveContextType(
        Type filterType,
        Type expectedContextType,
        Func<Type, bool> interfacePredicate,
        Func<Type, Type> contextSelector)
    {
        Type? updateContextType = null;

        foreach (var interfaceType in filterType.GetInterfaces())
        {
            if (!interfacePredicate(interfaceType))
            {
                continue;
            }

            var contextType = contextSelector(interfaceType);

            if (contextType == expectedContextType)
            {
                return contextType;
            }

            if (contextType == typeof(TelegramUpdateContext))
            {
                updateContextType = contextType;
            }
        }

        return updateContextType;
    }

    private static void ValidateConcreteFilterType(Type filterType)
    {
        ArgumentNullException.ThrowIfNull(filterType);

        if (filterType.IsInterface ||
            filterType.IsAbstract ||
            filterType.ContainsGenericParameters)
        {
            throw new InvalidOperationException(
                $"Custom Telegram filter type '{filterType.FullName}' must be a concrete closed type.");
        }
    }

    private static void ValidateResolvedContextType(
        Type filterType,
        Type contextType,
        Type expectedContextType)
    {
        ArgumentNullException.ThrowIfNull(contextType);

        if (contextType != expectedContextType &&
            contextType != typeof(TelegramUpdateContext))
        {
            throw new InvalidOperationException(
                $"Custom Telegram filter type '{filterType.FullName}' is not compatible with {expectedContextType.Name} handlers.");
        }
    }

    private static Type GetExpectedContextType(TelegramHandlerKind handlerKind)
    {
        return handlerKind switch
        {
            TelegramHandlerKind.Callback => typeof(CallbackQueryContext),
            TelegramHandlerKind.ChatMember => typeof(ChatMemberUpdatedContext),
            _ => typeof(MessageContext)
        };
    }

    private static TelegramCustomFilterInvoker CreateUntypedInvoker(Type contextType)
    {
        return (TelegramCustomFilterInvoker)UntypedInvokerMethod
            .MakeGenericMethod(contextType)
            .CreateDelegate(typeof(TelegramCustomFilterInvoker));
    }

    private static TelegramCustomFilterInvoker CreateTypedInvoker(
        Type contextType,
        Type attributeType)
    {
        return (TelegramCustomFilterInvoker)TypedInvokerMethod
            .MakeGenericMethod(contextType, attributeType)
            .CreateDelegate(typeof(TelegramCustomFilterInvoker));
    }

    private static ValueTask<bool> InvokeUntypedFilterAsync<TContext>(
        object filter,
        TelegramUpdateContext context,
        Attribute? attribute,
        CancellationToken cancellationToken)
        where TContext : TelegramUpdateContext
    {
        return ((ITelegramFilter<TContext>)filter).MatchesAsync((TContext)context, cancellationToken);
    }

    private static ValueTask<bool> InvokeTypedFilterAsync<TContext, TAttribute>(
        object filter,
        TelegramUpdateContext context,
        Attribute? attribute,
        CancellationToken cancellationToken)
        where TContext : TelegramUpdateContext
        where TAttribute : Attribute
    {
        return ((ITelegramFilter<TContext, TAttribute>)filter).MatchesAsync(
            (TContext)context,
            (TAttribute)attribute!,
            cancellationToken);
    }

    private delegate ValueTask<bool> TelegramCustomFilterInvoker(
        object filter,
        TelegramUpdateContext context,
        Attribute? attribute,
        CancellationToken cancellationToken);
}
