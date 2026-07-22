namespace TeleFlow.Telegram.Internal.Handlers;

internal enum TelegramFilterKind
{
    ChatType,
    ChatId,
    ChatUsername,
    FromUser,
    HasText,
    HasPhoto,
    HasDocument,
    HasCaption,
    HasVideo,
    HasAnimation,
    HasAudio,
    HasVoice,
    HasVideoNote,
    HasSticker,
    HasContact,
    HasLocation,
    HasVenue,
    HasPoll,
    HasDice,
    FromBot,
    FromPremiumUser,
    IsReply,
    ReplyToBot,
    MessageThreadId,
    HasMessageThread,
    HasCallbackData,
    CallbackDataPrefix,
    SenderChatType
}
