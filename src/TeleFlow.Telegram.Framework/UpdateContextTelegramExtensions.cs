using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Core.Updates;
using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public static class UpdateContextTelegramExtensions
{
    public static bool TryGetTelegramUpdate(
        this UpdateContext context,
        [NotNullWhen(true)] out Update? update)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Payload is TelegramUpdatePayload payload)
        {
            update = payload.Update;
            return true;
        }

        update = null;
        return false;
    }

    public static TelegramUpdateContext GetTelegramContext(this UpdateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Services
            .GetRequiredService<TelegramContextFactory>()
            .CreateTelegramContext(context);
    }

    public static MessageContext GetMessageContext(this UpdateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Services
            .GetRequiredService<TelegramContextFactory>()
            .CreateMessageContext(context);
    }

    public static CallbackQueryContext GetCallbackQueryContext(this UpdateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Services
            .GetRequiredService<TelegramContextFactory>()
            .CreateCallbackQueryContext(context);
    }

    public static ChatMemberUpdatedContext GetChatMemberUpdatedContext(this UpdateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Services
            .GetRequiredService<TelegramContextFactory>()
            .CreateChatMemberUpdatedContext(context);
    }
}
