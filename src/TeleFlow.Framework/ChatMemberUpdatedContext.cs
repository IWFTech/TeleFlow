using TeleFlow.Framework.Callbacks;
using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public sealed class ChatMemberUpdatedContext : TelegramUpdateContext
{
    internal ChatMemberUpdatedContext(
        TeleFlow.Framework.Updates.UpdateContext core,
        ITelegramClient bot,
        ICallbackDataSerializer callbackData,
        TelegramUpdatePayload payload,
        ChatMemberUpdated telegramChatMemberUpdated)
        : base(core, bot, callbackData, payload)
    {
        TelegramChatMemberUpdated = telegramChatMemberUpdated;
        TelegramChat = telegramChatMemberUpdated.Chat;
        Actor = telegramChatMemberUpdated.From;
        OldChatMember = telegramChatMemberUpdated.OldChatMember;
        NewChatMember = telegramChatMemberUpdated.NewChatMember;
        Member = TelegramChatMemberClassifier.GetUser(NewChatMember);
    }

    public ChatMemberUpdated TelegramChatMemberUpdated { get; }

    public Chat TelegramChat { get; }

    public User Actor { get; }

    public User Member { get; }

    public ChatMember OldChatMember { get; }

    public ChatMember NewChatMember { get; }
}
