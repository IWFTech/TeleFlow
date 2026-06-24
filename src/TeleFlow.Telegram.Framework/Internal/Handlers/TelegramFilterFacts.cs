using System.Reflection;
using TeleFlow.Annotations;

namespace TeleFlow.Telegram.Internal.Handlers;

internal enum TelegramFilterTarget
{
    Message,
    Callback,
    Chat,
    MessageThread
}

internal enum TelegramMarkerFilterGroup
{
    MessageContent,
    MessageSender,
    SharedMetadata
}

internal sealed record TelegramMarkerFilterSpec(
    Type AttributeType,
    TelegramFilterKind Kind,
    TelegramMarkerFilterGroup Group);

internal static class TelegramFilterFacts
{
    public static IReadOnlyList<TelegramMarkerFilterSpec> MarkerFilters { get; } = new TelegramMarkerFilterSpec[]
    {
        new(typeof(HasTextAttribute), TelegramFilterKind.HasText, TelegramMarkerFilterGroup.MessageContent),
        new(typeof(HasPhotoAttribute), TelegramFilterKind.HasPhoto, TelegramMarkerFilterGroup.MessageContent),
        new(typeof(HasDocumentAttribute), TelegramFilterKind.HasDocument, TelegramMarkerFilterGroup.MessageContent),
        new(typeof(HasCaptionAttribute), TelegramFilterKind.HasCaption, TelegramMarkerFilterGroup.MessageContent),
        new(typeof(HasVideoAttribute), TelegramFilterKind.HasVideo, TelegramMarkerFilterGroup.MessageContent),
        new(typeof(HasAnimationAttribute), TelegramFilterKind.HasAnimation, TelegramMarkerFilterGroup.MessageContent),
        new(typeof(HasAudioAttribute), TelegramFilterKind.HasAudio, TelegramMarkerFilterGroup.MessageContent),
        new(typeof(HasVoiceAttribute), TelegramFilterKind.HasVoice, TelegramMarkerFilterGroup.MessageContent),
        new(typeof(HasVideoNoteAttribute), TelegramFilterKind.HasVideoNote, TelegramMarkerFilterGroup.MessageContent),
        new(typeof(HasStickerAttribute), TelegramFilterKind.HasSticker, TelegramMarkerFilterGroup.MessageContent),
        new(typeof(HasContactAttribute), TelegramFilterKind.HasContact, TelegramMarkerFilterGroup.MessageContent),
        new(typeof(HasLocationAttribute), TelegramFilterKind.HasLocation, TelegramMarkerFilterGroup.MessageContent),
        new(typeof(HasVenueAttribute), TelegramFilterKind.HasVenue, TelegramMarkerFilterGroup.MessageContent),
        new(typeof(HasPollAttribute), TelegramFilterKind.HasPoll, TelegramMarkerFilterGroup.MessageContent),
        new(typeof(HasDiceAttribute), TelegramFilterKind.HasDice, TelegramMarkerFilterGroup.MessageContent),
        new(typeof(FromPremiumUserAttribute), TelegramFilterKind.FromPremiumUser, TelegramMarkerFilterGroup.MessageSender),
        new(typeof(IsReplyAttribute), TelegramFilterKind.IsReply, TelegramMarkerFilterGroup.MessageSender),
        new(typeof(ReplyToBotAttribute), TelegramFilterKind.ReplyToBot, TelegramMarkerFilterGroup.MessageSender),
        new(typeof(HasMessageThreadAttribute), TelegramFilterKind.HasMessageThread, TelegramMarkerFilterGroup.SharedMetadata),
        new(typeof(HasCallbackDataAttribute), TelegramFilterKind.HasCallbackData, TelegramMarkerFilterGroup.SharedMetadata)
    };

    public static IEnumerable<TelegramMarkerFilterSpec> GetMarkerFilters(TelegramMarkerFilterGroup group)
    {
        return MarkerFilters.Where(spec => spec.Group == group);
    }

    public static TelegramFilterTarget GetTarget(TelegramFilterKind kind)
    {
        return kind switch
        {
            TelegramFilterKind.ChatType or
                TelegramFilterKind.ChatId or
                TelegramFilterKind.ChatUsername => TelegramFilterTarget.Chat,

            TelegramFilterKind.MessageThreadId or
                TelegramFilterKind.HasMessageThread => TelegramFilterTarget.MessageThread,

            TelegramFilterKind.HasCallbackData or
                TelegramFilterKind.CallbackDataPrefix => TelegramFilterTarget.Callback,

            TelegramFilterKind.FromUser or
                TelegramFilterKind.HasText or
                TelegramFilterKind.HasPhoto or
                TelegramFilterKind.HasDocument or
                TelegramFilterKind.HasCaption or
                TelegramFilterKind.HasVideo or
                TelegramFilterKind.HasAnimation or
                TelegramFilterKind.HasAudio or
                TelegramFilterKind.HasVoice or
                TelegramFilterKind.HasVideoNote or
                TelegramFilterKind.HasSticker or
                TelegramFilterKind.HasContact or
                TelegramFilterKind.HasLocation or
                TelegramFilterKind.HasVenue or
                TelegramFilterKind.HasPoll or
                TelegramFilterKind.HasDice or
                TelegramFilterKind.FromBot or
                TelegramFilterKind.FromPremiumUser or
                TelegramFilterKind.IsReply or
                TelegramFilterKind.ReplyToBot => TelegramFilterTarget.Message,

            _ => throw new InvalidOperationException($"Unsupported Telegram filter kind '{kind}'.")
        };
    }

    public static string MapChatType(TelegramChatType chatType)
    {
        return chatType switch
        {
            TelegramChatType.Private => "private",
            TelegramChatType.Group => "group",
            TelegramChatType.Supergroup => "supergroup",
            TelegramChatType.Channel => "channel",
            TelegramChatType.Sender => "sender",
            _ => throw new InvalidOperationException($"Unsupported Telegram chat type '{chatType}'.")
        };
    }

    public static bool HasMarkerFilter(
        MemberInfo member,
        TelegramMarkerFilterSpec spec)
    {
        return member.GetCustomAttribute(spec.AttributeType, inherit: true) is not null;
    }
}
