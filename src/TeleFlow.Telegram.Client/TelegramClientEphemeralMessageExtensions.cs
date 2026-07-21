using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

/// <summary>
/// Provides explicit client-level conveniences for sending Telegram ephemeral text messages.
/// Framework context helpers use this surface after deriving an ephemeral target from an incoming update.
/// </summary>
public static class TelegramClientEphemeralMessageExtensions
{
    public static async Task<EphemeralMessageReference> SendEphemeralMessageAsync(
        this ITelegramClient bot,
        EphemeralMessageTarget target,
        string text,
        InlineKeyboardMarkup? replyMarkup = null,
        string? callbackQueryId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (callbackQueryId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(callbackQueryId);
        }

        var message = await bot.SendMessageAsync(
            target.ChatId,
            text,
            receiverUserId: target.ReceiverUserId,
            callbackQueryId: callbackQueryId,
            replyMarkup: replyMarkup is null ? null : ReplyMarkup.From(replyMarkup),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (message.EphemeralMessageId is not long ephemeralMessageId)
        {
            throw new InvalidOperationException(
                "Telegram returned an ephemeral message without an ephemeral_message_id.");
        }

        return new EphemeralMessageReference(bot, target, ephemeralMessageId, message);
    }
}
