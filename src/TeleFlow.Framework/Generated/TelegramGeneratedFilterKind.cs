using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure filter kind emitted by TeleFlow source generators.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum TelegramGeneratedFilterKind
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
    Custom,
    FromHuman,
    SenderChatType
}
