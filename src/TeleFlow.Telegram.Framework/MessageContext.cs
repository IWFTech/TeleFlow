using TeleFlow.Core.Callbacks;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public sealed class MessageContext : TelegramUpdateContext
{
    private readonly MessageActions _messageActions;

    internal MessageContext(
        TeleFlow.Core.Updates.UpdateContext core,
        ITelegramClient bot,
        ICallbackDataSerializer callbackData,
        TelegramUpdatePayload payload,
        Message telegramMessage)
        : base(core, bot, callbackData, payload)
    {
        TelegramMessage = telegramMessage;
        TelegramChat = telegramMessage.Chat;
        Sender = telegramMessage.From;
        User = Sender is null ? null : new TelegramUserInfo(Sender);
        _messageActions = new MessageActions(this);
    }

    public Message TelegramMessage { get; }

    public Chat TelegramChat { get; }

    public User? Sender { get; }

    public TelegramUserInfo? User { get; }

    public MessageActions Message => _messageActions;
}
