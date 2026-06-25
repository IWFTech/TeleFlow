namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed class TelegramHandlerTable
{
    public TelegramHandlerTable(IEnumerable<TelegramHandlerDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var orderedDescriptors = descriptors
            .OrderBy(static descriptor => descriptor.RegistrationOrder)
            .ToArray();

        CommandHandlers = OrderForRouteSelection(orderedDescriptors
            .Where(static descriptor => descriptor.Kind == TelegramHandlerKind.Command)
            .ToArray());
        MessageHandlers = OrderForRouteSelection(orderedDescriptors
            .Where(static descriptor => descriptor.Kind == TelegramHandlerKind.Message)
            .ToArray());
        CallbackHandlers = orderedDescriptors
            .Where(static descriptor => descriptor.Kind == TelegramHandlerKind.Callback)
            .ToArray();
        ChatMemberHandlers = OrderForRouteSelection(orderedDescriptors
            .Where(static descriptor => descriptor.Kind == TelegramHandlerKind.ChatMember)
            .ToArray());
        HasStatefulHandlers = orderedDescriptors.Any(static descriptor => descriptor.States.Count > 0);

        EnsureNoDuplicateCallbackPrefixes(CallbackHandlers);
    }

    public IReadOnlyList<TelegramHandlerDescriptor> CommandHandlers { get; }

    public IReadOnlyList<TelegramHandlerDescriptor> MessageHandlers { get; }

    public IReadOnlyList<TelegramHandlerDescriptor> CallbackHandlers { get; }

    public IReadOnlyList<TelegramHandlerDescriptor> ChatMemberHandlers { get; }

    public bool HasStatefulHandlers { get; }

    private static void EnsureNoDuplicateCallbackPrefixes(IReadOnlyList<TelegramHandlerDescriptor> callbackHandlers)
    {
        var duplicatePrefix = callbackHandlers
            .Where(static handler => handler.CallbackPayloadType is not null)
            .Select(static handler => new
            {
                Handler = handler,
                HasMetadata = CallbackDataMetadata.TryCreate(handler.CallbackPayloadType!, out var metadata),
                Metadata = metadata
            })
            .Where(static item => item.HasMetadata)
            .GroupBy(static item => item.Metadata.Prefix, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicatePrefix is null)
        {
            return;
        }

        var handlers = string.Join(
            ", ",
            duplicatePrefix.Select(static item => TelegramHandlerDescriptorFormatter.GetDisplayName(item.Handler)));

        throw new InvalidOperationException(
            $"Duplicate Telegram callback data prefix '{duplicatePrefix.Key}' is used by multiple handlers: {handlers}.");
    }

    private static TelegramHandlerDescriptor[] OrderForRouteSelection(IEnumerable<TelegramHandlerDescriptor> handlers)
    {
        return handlers
            .OrderBy(static handler => GetRoutePriority(handler.Route))
            .ThenByDescending(static handler => handler.Route.Specificity)
            .ThenBy(static handler => handler.RegistrationOrder)
            .ToArray();
    }

    private static int GetRoutePriority(TelegramRouteDescriptor route)
    {
        return route.RouteKind switch
        {
            TelegramRouteKind.CommandExact => 0,
            TelegramRouteKind.CommandTemplate => 1,
            TelegramRouteKind.CommandRegex => 2,
            TelegramRouteKind.TextExact => 10,
            TelegramRouteKind.MessageAny when route.TextFilters.Count > 0 => 10,
            TelegramRouteKind.TextTemplate => 11,
            TelegramRouteKind.TextRegex => 12,
            TelegramRouteKind.MessageAny => 13,
            TelegramRouteKind.ChatMemberUpdated or
                TelegramRouteKind.MyChatMemberUpdated when route.ChatMemberTransitions.Count > 0 => 0,
            TelegramRouteKind.ChatMemberUpdated or
                TelegramRouteKind.MyChatMemberUpdated => 1,
            _ => 100
        };
    }
}
