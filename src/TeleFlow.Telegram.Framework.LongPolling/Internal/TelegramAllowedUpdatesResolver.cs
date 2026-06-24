using Microsoft.Extensions.Logging;
using TeleFlow.Telegram.Internal.Handlers;

namespace TeleFlow.Telegram.Internal;

internal static partial class TelegramAllowedUpdatesResolver
{
    public static IReadOnlyList<string>? Resolve(
        TelegramAllowedUpdates allowedUpdates,
        IReadOnlyList<TelegramHandlerDescriptor> handlerDescriptors,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(allowedUpdates);
        ArgumentNullException.ThrowIfNull(handlerDescriptors);
        ArgumentNullException.ThrowIfNull(logger);

        return allowedUpdates.Mode switch
        {
            TelegramAllowedUpdatesMode.Auto => ResolveAuto(handlerDescriptors, logger),
            TelegramAllowedUpdatesMode.All => TelegramUpdateType.AllKnown.Select(static item => item.Value).ToArray(),
            TelegramAllowedUpdatesMode.Only => allowedUpdates.UpdateTypes.Select(static item => item.Value).ToArray(),
            _ => throw new InvalidOperationException("Unsupported Telegram allowed updates mode.")
        };
    }

    private static List<string>? ResolveAuto(
        IReadOnlyList<TelegramHandlerDescriptor> handlerDescriptors,
        ILogger logger)
    {
        if (handlerDescriptors.Count == 0)
        {
            LogAutoInferenceSkipped(logger);
            return null;
        }

        var values = new List<string>(capacity: 4);

        if (handlerDescriptors.Any(static descriptor =>
                descriptor.Kind is TelegramHandlerKind.Command or TelegramHandlerKind.Message))
        {
            values.Add(TelegramUpdateType.Message.Value);
        }

        if (handlerDescriptors.Any(static descriptor => descriptor.Kind == TelegramHandlerKind.Callback))
        {
            values.Add(TelegramUpdateType.CallbackQuery.Value);
        }

        if (handlerDescriptors.Any(static descriptor => descriptor.Route.RouteKind == TelegramRouteKind.MyChatMemberUpdated))
        {
            values.Add(TelegramUpdateType.MyChatMember.Value);
        }

        if (handlerDescriptors.Any(static descriptor => descriptor.Route.RouteKind == TelegramRouteKind.ChatMemberUpdated))
        {
            values.Add(TelegramUpdateType.ChatMember.Value);
        }

        return values.Count == 0 ? null : values;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Telegram allowed_updates auto inference has no handler metadata. getUpdates will leave allowed_updates unset.")]
    private static partial void LogAutoInferenceSkipped(ILogger logger);
}
