using TeleFlow.Framework.States;
using TeleFlow.Framework.Updates;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Adapts the Core current-update accessor to Telegram-specific contexts and identity properties for scoped
/// application services reached while a Telegram update is being dispatched.
/// </summary>
internal sealed class TelegramCurrentUpdateAccessor(
    IUpdateContextAccessor currentUpdate,
    TelegramContextFactory contextFactory) : ITelegramCurrentUpdateAccessor
{
    private bool _updateCached;
    private Update? _update;
    private bool _classificationCached;
    private TelegramUpdateClassification _classification;

    public bool IsAvailable => TryGetUpdate(out _);

    public TelegramUpdateContext Current => TryGetCurrent(out var context)
        ? context
        : throw new InvalidOperationException(
            "No current Telegram update is available. ITelegramCurrentUpdateAccessor can only be used inside TeleFlow Telegram update processing.");

    public Update Update => TryGetUpdate(out var update)
        ? update
        : throw new InvalidOperationException(
            "No current Telegram update is available. ITelegramCurrentUpdateAccessor can only be used inside TeleFlow Telegram update processing.");

    public User? User
    {
        get
        {
            return TryGetClassification(out var classification)
                ? classification.User
                : null;
        }
    }

    public Chat? Chat
    {
        get
        {
            return TryGetClassification(out var classification)
                ? classification.Chat
                : null;
        }
    }

    public Message? Message => TryGetUpdate(out var update) ? update.Message : null;

    public CallbackQuery? CallbackQuery => TryGetUpdate(out var update) ? update.CallbackQuery : null;

    public ChatMemberUpdated? ChatMemberUpdated
    {
        get
        {
            if (!TryGetUpdate(out var update))
            {
                return null;
            }

            return update.ChatMember ?? update.MyChatMember;
        }
    }

    public StateKey? StateKey
    {
        get
        {
            if (!currentUpdate.TryGetCurrent(out var context) ||
                !context.TryGetState(out var state))
            {
                return null;
            }

            return state.Key;
        }
    }

    public bool TryGetCurrent(out TelegramUpdateContext context)
    {
        if (!currentUpdate.TryGetCurrent(out var updateContext) ||
            updateContext.Payload is not TelegramUpdatePayload payload)
        {
            context = null!;
            return false;
        }

        if (payload.Update.Message is not null)
        {
            context = contextFactory.CreateMessageContext(updateContext);
            return true;
        }

        if (payload.Update.CallbackQuery is not null)
        {
            context = contextFactory.CreateCallbackQueryContext(updateContext);
            return true;
        }

        if (payload.Update.ChatMember is not null ||
            payload.Update.MyChatMember is not null)
        {
            context = contextFactory.CreateChatMemberUpdatedContext(updateContext);
            return true;
        }

        context = contextFactory.CreateTelegramContext(updateContext);
        return true;
    }

    public bool TryGetMessageContext(out MessageContext context)
    {
        if (!currentUpdate.TryGetCurrent(out var updateContext) ||
            updateContext.Payload is not TelegramUpdatePayload payload ||
            payload.Update.Message is null)
        {
            context = null!;
            return false;
        }

        context = contextFactory.CreateMessageContext(updateContext);
        return true;
    }

    public bool TryGetCallbackQueryContext(out CallbackQueryContext context)
    {
        if (!currentUpdate.TryGetCurrent(out var updateContext) ||
            updateContext.Payload is not TelegramUpdatePayload payload ||
            payload.Update.CallbackQuery is null)
        {
            context = null!;
            return false;
        }

        context = contextFactory.CreateCallbackQueryContext(updateContext);
        return true;
    }

    public bool TryGetChatMemberUpdatedContext(out ChatMemberUpdatedContext context)
    {
        if (!currentUpdate.TryGetCurrent(out var updateContext) ||
            updateContext.Payload is not TelegramUpdatePayload payload ||
            payload.Update.ChatMember is null && payload.Update.MyChatMember is null)
        {
            context = null!;
            return false;
        }

        context = contextFactory.CreateChatMemberUpdatedContext(updateContext);
        return true;
    }

    private bool TryGetUpdate(out Update update)
    {
        if (_updateCached)
        {
            update = _update!;
            return true;
        }

        if (!currentUpdate.TryGetCurrent(out var context) ||
            context.Payload is not TelegramUpdatePayload payload)
        {
            update = null!;
            return false;
        }

        _update = payload.Update;
        _updateCached = true;
        update = _update;
        return true;
    }

    private bool TryGetClassification(out TelegramUpdateClassification classification)
    {
        if (_classificationCached)
        {
            classification = _classification;
            return true;
        }

        if (!TryGetUpdate(out var update))
        {
            classification = default;
            return false;
        }

        _classification = TelegramUpdateClassifier.Classify(update);
        _classificationCached = true;
        classification = _classification;
        return true;
    }
}
