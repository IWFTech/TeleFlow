using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Classifies one incoming Telegram update and extracts the user and chat identities exposed to scoped application code.
/// The explicit property order mirrors the generated <see cref="Update"/> schema and performs no reflection at runtime.
/// </summary>
internal static class TelegramUpdateClassifier
{
    public static TelegramUpdateClassification Classify(Update update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (update.Message is { } message) return FromMessage("message", message);
        if (update.EditedMessage is { } editedMessage) return FromMessage("edited_message", editedMessage);
        if (update.ChannelPost is { } channelPost) return FromMessage("channel_post", channelPost);
        if (update.EditedChannelPost is { } editedChannelPost) return FromMessage("edited_channel_post", editedChannelPost);
        if (update.BusinessConnection is { } businessConnection)
        {
            return new TelegramUpdateClassification("business_connection", businessConnection.User, null);
        }

        if (update.BusinessMessage is { } businessMessage) return FromMessage("business_message", businessMessage);
        if (update.EditedBusinessMessage is { } editedBusinessMessage)
        {
            return FromMessage("edited_business_message", editedBusinessMessage);
        }

        if (update.DeletedBusinessMessages is { } deletedBusinessMessages)
        {
            return new TelegramUpdateClassification("deleted_business_messages", null, deletedBusinessMessages.Chat);
        }

        if (update.GuestMessage is { } guestMessage) return FromMessage("guest_message", guestMessage);
        if (update.MessageReaction is { } messageReaction)
        {
            return new TelegramUpdateClassification("message_reaction", messageReaction.User, messageReaction.Chat);
        }

        if (update.MessageReactionCount is { } messageReactionCount)
        {
            return new TelegramUpdateClassification("message_reaction_count", null, messageReactionCount.Chat);
        }

        if (update.InlineQuery is { } inlineQuery)
        {
            return new TelegramUpdateClassification("inline_query", inlineQuery.From, null);
        }

        if (update.ChosenInlineResult is { } chosenInlineResult)
        {
            return new TelegramUpdateClassification("chosen_inline_result", chosenInlineResult.From, null);
        }

        if (update.CallbackQuery is { } callbackQuery)
        {
            return new TelegramUpdateClassification("callback_query", callbackQuery.From, GetCallbackChat(callbackQuery));
        }

        if (update.ShippingQuery is { } shippingQuery)
        {
            return new TelegramUpdateClassification("shipping_query", shippingQuery.From, null);
        }

        if (update.PreCheckoutQuery is { } preCheckoutQuery)
        {
            return new TelegramUpdateClassification("pre_checkout_query", preCheckoutQuery.From, null);
        }

        if (update.PurchasedPaidMedia is { } purchasedPaidMedia)
        {
            return new TelegramUpdateClassification("purchased_paid_media", purchasedPaidMedia.From, null);
        }

        if (update.Poll is not null) return new TelegramUpdateClassification("poll", null, null);
        if (update.PollAnswer is { } pollAnswer)
        {
            return new TelegramUpdateClassification("poll_answer", pollAnswer.User, pollAnswer.VoterChat);
        }

        if (update.MyChatMember is { } myChatMember)
        {
            return new TelegramUpdateClassification("my_chat_member", myChatMember.From, myChatMember.Chat);
        }

        if (update.ChatMember is { } chatMember)
        {
            return new TelegramUpdateClassification("chat_member", chatMember.From, chatMember.Chat);
        }

        if (update.ChatJoinRequest is { } chatJoinRequest)
        {
            return new TelegramUpdateClassification("chat_join_request", chatJoinRequest.From, chatJoinRequest.Chat);
        }

        if (update.ChatBoost is { } chatBoost)
        {
            return new TelegramUpdateClassification(
                "chat_boost",
                GetChatBoostUser(chatBoost.Boost?.Source),
                chatBoost.Chat);
        }

        if (update.RemovedChatBoost is { } removedChatBoost)
        {
            return new TelegramUpdateClassification(
                "removed_chat_boost",
                GetChatBoostUser(removedChatBoost.Source),
                removedChatBoost.Chat);
        }

        if (update.ManagedBot is { } managedBot)
        {
            return new TelegramUpdateClassification("managed_bot", managedBot.User, null);
        }

        if (update.Subscription is { } subscription)
        {
            return new TelegramUpdateClassification("subscription", subscription.User, null);
        }

        return new TelegramUpdateClassification("unknown", null, null);
    }

    private static TelegramUpdateClassification FromMessage(string type, Message message)
    {
        return new TelegramUpdateClassification(type, message.From, message.Chat);
    }

    private static Chat? GetCallbackChat(CallbackQuery callbackQuery)
    {
        return callbackQuery.Message?.Message?.Chat ??
               callbackQuery.Message?.InaccessibleMessage?.Chat;
    }

    private static User? GetChatBoostUser(ChatBoostSource? source)
    {
        return source?.ChatBoostSourcePremium?.User ??
               source?.ChatBoostSourceGiftCode?.User ??
               source?.ChatBoostSourceGiveaway?.User;
    }
}

/// <summary>
/// Carries the Bot API update type and identity selected by <see cref="TelegramUpdateClassifier"/> without allocations.
/// </summary>
internal readonly record struct TelegramUpdateClassification(string Type, User? User, Chat? Chat);
