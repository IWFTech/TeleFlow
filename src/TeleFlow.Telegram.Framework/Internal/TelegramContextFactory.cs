using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Core.Callbacks;
using TeleFlow.Core.Updates;

namespace TeleFlow.Telegram.Internal;

internal sealed class TelegramContextFactory
{
    private static readonly object TelegramContextKey = new();
    private static readonly object MessageContextKey = new();
    private static readonly object CallbackQueryContextKey = new();
    private static readonly object ChatMemberUpdatedContextKey = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Context factory methods remain instance methods because the factory is resolved through DI as one coherent service.")]
    public TelegramUpdateContext CreateTelegramContext(UpdateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items.TryGetValue(TelegramContextKey, out var cachedContext) &&
            cachedContext is TelegramUpdateContext telegramContext)
        {
            return telegramContext;
        }

        var payload = GetPayload(context);
        var rootBot = context.Services.GetRequiredService<ITelegramClient>();
        var bot = new UpdateScopedTelegramClient(rootBot, context.CancellationToken);
        var callbackData = context.Services.GetRequiredService<ICallbackDataSerializer>();
        telegramContext = new TelegramUpdateContext(context, bot, callbackData, payload);
        context.Items[TelegramContextKey] = telegramContext;
        return telegramContext;
    }

    public MessageContext CreateMessageContext(UpdateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items.TryGetValue(MessageContextKey, out var cachedContext) &&
            cachedContext is MessageContext messageContext)
        {
            return messageContext;
        }

        var telegramContext = CreateTelegramContext(context);
        var telegramMessage = telegramContext.Update.Message
            ?? throw new InvalidOperationException("The current update does not contain a Telegram message.");

        messageContext = new MessageContext(
            context,
            telegramContext.Bot,
            telegramContext.CallbackData,
            telegramContext.Payload,
            telegramMessage);
        context.Items[MessageContextKey] = messageContext;
        return messageContext;
    }

    public CallbackQueryContext CreateCallbackQueryContext(UpdateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items.TryGetValue(CallbackQueryContextKey, out var cachedContext) &&
            cachedContext is CallbackQueryContext callbackQueryContext)
        {
            return callbackQueryContext;
        }

        var telegramContext = CreateTelegramContext(context);
        var telegramCallbackQuery = telegramContext.Update.CallbackQuery
            ?? throw new InvalidOperationException("The current update does not contain a Telegram callback query.");

        callbackQueryContext = new CallbackQueryContext(
            context,
            telegramContext.Bot,
            telegramContext.CallbackData,
            telegramContext.Payload,
            telegramCallbackQuery);

        context.Items[CallbackQueryContextKey] = callbackQueryContext;
        return callbackQueryContext;
    }

    public ChatMemberUpdatedContext CreateChatMemberUpdatedContext(UpdateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items.TryGetValue(ChatMemberUpdatedContextKey, out var cachedContext) &&
            cachedContext is ChatMemberUpdatedContext chatMemberUpdatedContext)
        {
            return chatMemberUpdatedContext;
        }

        var telegramContext = CreateTelegramContext(context);
        var telegramChatMemberUpdated = telegramContext.Update.ChatMember ??
                                        telegramContext.Update.MyChatMember ??
                                        throw new InvalidOperationException("The current update does not contain a Telegram chat member update.");

        chatMemberUpdatedContext = new ChatMemberUpdatedContext(
            context,
            telegramContext.Bot,
            telegramContext.CallbackData,
            telegramContext.Payload,
            telegramChatMemberUpdated);

        context.Items[ChatMemberUpdatedContextKey] = chatMemberUpdatedContext;
        return chatMemberUpdatedContext;
    }

    private static TelegramUpdatePayload GetPayload(UpdateContext context)
    {
        if (context.Payload is TelegramUpdatePayload payload)
        {
            return payload;
        }

        throw new InvalidOperationException("The current update payload is not a Telegram update payload.");
    }
}
