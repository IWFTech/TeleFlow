using TeleFlow.Core.Callbacks;
using TeleFlow.Core.States;
using TeleFlow.Core.Updates;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public class TelegramUpdateContext
{
    private readonly ChatActions _chatActions;

    internal TelegramUpdateContext(
        UpdateContext core,
        ITelegramClient bot,
        ICallbackDataSerializer callbackData,
        TelegramUpdatePayload payload)
    {
        Core = core;
        Bot = bot;
        CallbackData = callbackData;
        Payload = payload;
        _chatActions = new ChatActions(this);
    }

    public UpdateContext Core { get; }

    public IServiceProvider Services => Core.Services;

    public CancellationToken CancellationToken => Core.CancellationToken;

    public ITelegramClient Bot { get; }

    public ICallbackDataSerializer CallbackData { get; }

    public ChatActions Chat => _chatActions;

    public UpdateState State => Core.GetState();

    public UpdateWizard Wizard => State.Wizard;

    public Update Update => Payload.Update;

    internal TelegramUpdatePayload Payload { get; }
}
