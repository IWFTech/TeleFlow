using TeleFlow.Telegram.Internal.Handlers;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

internal static class TelegramUpdateLogFormatter
{
    public static string GetUpdateType(Update update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (update.Message is not null) return "message";
        if (update.EditedMessage is not null) return "edited_message";
        if (update.ChannelPost is not null) return "channel_post";
        if (update.EditedChannelPost is not null) return "edited_channel_post";
        if (update.BusinessConnection is not null) return "business_connection";
        if (update.BusinessMessage is not null) return "business_message";
        if (update.EditedBusinessMessage is not null) return "edited_business_message";
        if (update.DeletedBusinessMessages is not null) return "deleted_business_messages";
        if (update.GuestMessage is not null) return "guest_message";
        if (update.MessageReaction is not null) return "message_reaction";
        if (update.MessageReactionCount is not null) return "message_reaction_count";
        if (update.InlineQuery is not null) return "inline_query";
        if (update.ChosenInlineResult is not null) return "chosen_inline_result";
        if (update.CallbackQuery is not null) return "callback_query";
        if (update.ShippingQuery is not null) return "shipping_query";
        if (update.PreCheckoutQuery is not null) return "pre_checkout_query";
        if (update.PurchasedPaidMedia is not null) return "purchased_paid_media";
        if (update.Poll is not null) return "poll";
        if (update.PollAnswer is not null) return "poll_answer";
        if (update.MyChatMember is not null) return "my_chat_member";
        if (update.ChatMember is not null) return "chat_member";
        if (update.ChatJoinRequest is not null) return "chat_join_request";
        if (update.ChatBoost is not null) return "chat_boost";
        if (update.RemovedChatBoost is not null) return "removed_chat_boost";
        if (update.ManagedBot is not null) return "managed_bot";

        return "unknown";
    }

    public static string FormatAllowedUpdates(IReadOnlyList<string>? allowedUpdates)
    {
        return allowedUpdates is null || allowedUpdates.Count == 0
            ? "unset"
            : $"[{string.Join(",", allowedUpdates)}]";
    }

    public static string FormatHandler(TelegramHandlerDescriptor handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return $"{handler.HandlerType.Name}.{handler.MethodName}";
    }

    public static string FormatRoute(TelegramRouteDescriptor route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return route.RouteKind switch
        {
            TelegramRouteKind.CommandExact => FormatPatternRoute(route, "CommandExact"),
            TelegramRouteKind.CommandTemplate => FormatPatternRoute(route, "CommandTemplate"),
            TelegramRouteKind.CommandRegex => FormatPatternRoute(route, "CommandRegex"),
            TelegramRouteKind.TextExact => FormatPatternRoute(route, "TextExact"),
            TelegramRouteKind.TextTemplate => FormatPatternRoute(route, "TextTemplate"),
            TelegramRouteKind.TextRegex => FormatPatternRoute(route, "TextRegex"),
            TelegramRouteKind.Callback when route.CallbackPayloadType is not null =>
                $"Callback<{route.CallbackPayloadType.Name}>",
            TelegramRouteKind.Callback => "Callback",
            TelegramRouteKind.MessageAny when route.TextFilters.Count > 0 =>
                $"MessageAny(text_filters={route.TextFilters.Count})",
            TelegramRouteKind.MessageAny => "MessageAny",
            _ => route.RouteKind.ToString()
        };
    }

    private static string FormatPatternRoute(TelegramRouteDescriptor route, string name)
    {
        return string.IsNullOrWhiteSpace(route.Pattern)
            ? name
            : $"{name}('{route.Pattern}')";
    }
}
