using TeleFlow.Framework.Callbacks;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public sealed class CallbackQueryContext : TelegramUpdateContext
{
    private readonly CallbackQueryActions _callbackActions;

    internal CallbackQueryContext(
        TeleFlow.Framework.Updates.UpdateContext core,
        ITelegramClient bot,
        ICallbackDataSerializer callbackData,
        TelegramUpdatePayload payload,
        CallbackQuery telegramCallbackQuery)
        : base(core, bot, callbackData, payload)
    {
        TelegramCallbackQuery = telegramCallbackQuery;
        Sender = telegramCallbackQuery.From;
        User = new TelegramUserInfo(Sender);
        _callbackActions = new CallbackQueryActions(this);
    }

    public CallbackQuery TelegramCallbackQuery { get; }

    public User Sender { get; }

    public TelegramUserInfo User { get; }

    public CallbackQueryActions Callback => _callbackActions;

    internal bool IsCallbackQueryAnswered { get; private set; }

    internal void MarkCallbackQueryAnswered()
    {
        IsCallbackQueryAnswered = true;
    }
}
