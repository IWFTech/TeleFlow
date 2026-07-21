using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

/// <summary>
/// Represents an ephemeral message sent through the Telegram Bot API.
/// It retains the addressing identity required by Telegram's dedicated ephemeral edit and delete methods.
/// </summary>
public sealed class EphemeralMessageReference
{
    private readonly ITelegramClient _bot;

    internal EphemeralMessageReference(
        ITelegramClient bot,
        EphemeralMessageTarget target,
        long ephemeralMessageId,
        Message telegramMessage)
    {
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(telegramMessage);

        _bot = bot;
        Target = target;
        EphemeralMessageId = ephemeralMessageId;
        TelegramMessage = telegramMessage;
    }

    public EphemeralMessageTarget Target { get; }

    public long ReceiverUserId => Target.ReceiverUserId;

    public long EphemeralMessageId { get; }

    public Message TelegramMessage { get; }

    public Task<bool> EditTextAsync(
        string text,
        InlineKeyboardMarkup? replyMarkup = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return _bot.EditEphemeralMessageTextAsync(
            chatId: Target.ChatId,
            receiverUserId: ReceiverUserId,
            ephemeralMessageId: EphemeralMessageId,
            text: text,
            replyMarkup: replyMarkup,
            cancellationToken: cancellationToken);
    }

    public Task<bool> EditCaptionAsync(
        string? caption = null,
        TelegramParseMode? parseMode = null,
        IReadOnlyList<MessageEntity>? captionEntities = null,
        InlineKeyboardMarkup? replyMarkup = null,
        CancellationToken cancellationToken = default)
    {
        return _bot.EditEphemeralMessageCaptionAsync(
            chatId: Target.ChatId,
            receiverUserId: ReceiverUserId,
            ephemeralMessageId: EphemeralMessageId,
            caption: caption,
            parseMode: parseMode,
            captionEntities: captionEntities,
            replyMarkup: replyMarkup,
            cancellationToken: cancellationToken);
    }

    public Task<bool> EditMediaAsync(
        InputMedia media,
        InlineKeyboardMarkup? replyMarkup = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(media);

        return _bot.EditEphemeralMessageMediaAsync(
            chatId: Target.ChatId,
            receiverUserId: ReceiverUserId,
            ephemeralMessageId: EphemeralMessageId,
            media: media,
            replyMarkup: replyMarkup,
            cancellationToken: cancellationToken);
    }

    public Task<bool> EditReplyMarkupAsync(
        InlineKeyboardMarkup? replyMarkup = null,
        CancellationToken cancellationToken = default)
    {
        return _bot.EditEphemeralMessageReplyMarkupAsync(
            chatId: Target.ChatId,
            receiverUserId: ReceiverUserId,
            ephemeralMessageId: EphemeralMessageId,
            replyMarkup: replyMarkup,
            cancellationToken: cancellationToken);
    }

    public Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
    {
        return _bot.DeleteEphemeralMessageAsync(
            chatId: Target.ChatId,
            receiverUserId: ReceiverUserId,
            ephemeralMessageId: EphemeralMessageId,
            cancellationToken: cancellationToken);
    }
}
