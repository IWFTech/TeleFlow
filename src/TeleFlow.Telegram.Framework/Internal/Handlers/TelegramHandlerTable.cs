namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed class TelegramHandlerTable
{
    public TelegramHandlerTable(IEnumerable<TelegramHandlerDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var orderedDescriptors = descriptors
            .OrderBy(static descriptor => descriptor.RegistrationOrder)
            .ToArray();

        CommandHandlers = orderedDescriptors
            .Where(static descriptor => descriptor.Kind == TelegramHandlerKind.Command)
            .ToArray();
        MessageHandlers = orderedDescriptors
            .Where(static descriptor => descriptor.Kind == TelegramHandlerKind.Message)
            .ToArray();
        CallbackHandlers = orderedDescriptors
            .Where(static descriptor => descriptor.Kind == TelegramHandlerKind.Callback)
            .ToArray();
        ChatMemberHandlers = orderedDescriptors
            .Where(static descriptor => descriptor.Kind == TelegramHandlerKind.ChatMember)
            .ToArray();

        EnsureNoDuplicateCallbackPrefixes(CallbackHandlers);
    }

    public IReadOnlyList<TelegramHandlerDescriptor> CommandHandlers { get; }

    public IReadOnlyList<TelegramHandlerDescriptor> MessageHandlers { get; }

    public IReadOnlyList<TelegramHandlerDescriptor> CallbackHandlers { get; }

    public IReadOnlyList<TelegramHandlerDescriptor> ChatMemberHandlers { get; }

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
}
