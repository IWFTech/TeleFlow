using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public sealed partial class MessageActions
{
    public Task<Message> AnswerPhotoAsync(
        InputFileString photo,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        return SendPhotoAsync(photo, caption, parseMode: null, replyMarkup: null, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerPhotoAsync(
        InputFileString photo,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        return SendPhotoAsync(photo, caption, parseMode, replyMarkup: null, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerPhotoAsync(
        InputFileString photo,
        ReplyMarkup replyMarkup,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendPhotoAsync(photo, caption, parseMode: null, replyMarkup, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerPhotoAsync(
        InputFileString photo,
        ReplyMarkup replyMarkup,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendPhotoAsync(photo, caption, parseMode, replyMarkup, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> ReplyPhotoAsync(
        InputFileString photo,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        return SendPhotoAsync(photo, caption, parseMode: null, replyMarkup: null, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyPhotoAsync(
        InputFileString photo,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        return SendPhotoAsync(photo, caption, parseMode, replyMarkup: null, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyPhotoAsync(
        InputFileString photo,
        ReplyMarkup replyMarkup,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendPhotoAsync(photo, caption, parseMode: null, replyMarkup, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyPhotoAsync(
        InputFileString photo,
        ReplyMarkup replyMarkup,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendPhotoAsync(photo, caption, parseMode, replyMarkup, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> AnswerDocumentAsync(
        InputFileString document,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        return SendDocumentAsync(document, caption, parseMode: null, replyMarkup: null, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerDocumentAsync(
        InputFileString document,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        return SendDocumentAsync(document, caption, parseMode, replyMarkup: null, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerDocumentAsync(
        InputFileString document,
        ReplyMarkup replyMarkup,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendDocumentAsync(document, caption, parseMode: null, replyMarkup, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerDocumentAsync(
        InputFileString document,
        ReplyMarkup replyMarkup,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendDocumentAsync(document, caption, parseMode, replyMarkup, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> ReplyDocumentAsync(
        InputFileString document,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        return SendDocumentAsync(document, caption, parseMode: null, replyMarkup: null, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyDocumentAsync(
        InputFileString document,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        return SendDocumentAsync(document, caption, parseMode, replyMarkup: null, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyDocumentAsync(
        InputFileString document,
        ReplyMarkup replyMarkup,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendDocumentAsync(document, caption, parseMode: null, replyMarkup, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyDocumentAsync(
        InputFileString document,
        ReplyMarkup replyMarkup,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendDocumentAsync(document, caption, parseMode, replyMarkup, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> AnswerVideoAsync(
        InputFileString video,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        return SendVideoAsync(video, caption, parseMode: null, replyMarkup: null, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerVideoAsync(
        InputFileString video,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        return SendVideoAsync(video, caption, parseMode, replyMarkup: null, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerVideoAsync(
        InputFileString video,
        ReplyMarkup replyMarkup,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendVideoAsync(video, caption, parseMode: null, replyMarkup, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerVideoAsync(
        InputFileString video,
        ReplyMarkup replyMarkup,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendVideoAsync(video, caption, parseMode, replyMarkup, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> ReplyVideoAsync(
        InputFileString video,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        return SendVideoAsync(video, caption, parseMode: null, replyMarkup: null, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyVideoAsync(
        InputFileString video,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        return SendVideoAsync(video, caption, parseMode, replyMarkup: null, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyVideoAsync(
        InputFileString video,
        ReplyMarkup replyMarkup,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendVideoAsync(video, caption, parseMode: null, replyMarkup, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyVideoAsync(
        InputFileString video,
        ReplyMarkup replyMarkup,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendVideoAsync(video, caption, parseMode, replyMarkup, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> AnswerAnimationAsync(
        InputFileString animation,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        return SendAnimationAsync(animation, caption, parseMode: null, replyMarkup: null, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerAnimationAsync(
        InputFileString animation,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        return SendAnimationAsync(animation, caption, parseMode, replyMarkup: null, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerAnimationAsync(
        InputFileString animation,
        ReplyMarkup replyMarkup,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendAnimationAsync(animation, caption, parseMode: null, replyMarkup, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerAnimationAsync(
        InputFileString animation,
        ReplyMarkup replyMarkup,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendAnimationAsync(animation, caption, parseMode, replyMarkup, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> ReplyAnimationAsync(
        InputFileString animation,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        return SendAnimationAsync(animation, caption, parseMode: null, replyMarkup: null, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyAnimationAsync(
        InputFileString animation,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        return SendAnimationAsync(animation, caption, parseMode, replyMarkup: null, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyAnimationAsync(
        InputFileString animation,
        ReplyMarkup replyMarkup,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendAnimationAsync(animation, caption, parseMode: null, replyMarkup, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyAnimationAsync(
        InputFileString animation,
        ReplyMarkup replyMarkup,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendAnimationAsync(animation, caption, parseMode, replyMarkup, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> AnswerAudioAsync(
        InputFileString audio,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        return SendAudioAsync(audio, caption, parseMode: null, replyMarkup: null, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerAudioAsync(
        InputFileString audio,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        return SendAudioAsync(audio, caption, parseMode, replyMarkup: null, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerAudioAsync(
        InputFileString audio,
        ReplyMarkup replyMarkup,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendAudioAsync(audio, caption, parseMode: null, replyMarkup, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerAudioAsync(
        InputFileString audio,
        ReplyMarkup replyMarkup,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendAudioAsync(audio, caption, parseMode, replyMarkup, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> ReplyAudioAsync(
        InputFileString audio,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        return SendAudioAsync(audio, caption, parseMode: null, replyMarkup: null, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyAudioAsync(
        InputFileString audio,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        return SendAudioAsync(audio, caption, parseMode, replyMarkup: null, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyAudioAsync(
        InputFileString audio,
        ReplyMarkup replyMarkup,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendAudioAsync(audio, caption, parseMode: null, replyMarkup, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyAudioAsync(
        InputFileString audio,
        ReplyMarkup replyMarkup,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendAudioAsync(audio, caption, parseMode, replyMarkup, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> AnswerVoiceAsync(
        InputFileString voice,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        return SendVoiceAsync(voice, caption, parseMode: null, replyMarkup: null, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerVoiceAsync(
        InputFileString voice,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        return SendVoiceAsync(voice, caption, parseMode, replyMarkup: null, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerVoiceAsync(
        InputFileString voice,
        ReplyMarkup replyMarkup,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendVoiceAsync(voice, caption, parseMode: null, replyMarkup, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerVoiceAsync(
        InputFileString voice,
        ReplyMarkup replyMarkup,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendVoiceAsync(voice, caption, parseMode, replyMarkup, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> ReplyVoiceAsync(
        InputFileString voice,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        return SendVoiceAsync(voice, caption, parseMode: null, replyMarkup: null, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyVoiceAsync(
        InputFileString voice,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        return SendVoiceAsync(voice, caption, parseMode, replyMarkup: null, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyVoiceAsync(
        InputFileString voice,
        ReplyMarkup replyMarkup,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendVoiceAsync(voice, caption, parseMode: null, replyMarkup, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyVoiceAsync(
        InputFileString voice,
        ReplyMarkup replyMarkup,
        string? caption,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendVoiceAsync(voice, caption, parseMode, replyMarkup, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> AnswerStickerAsync(
        InputFileString sticker,
        CancellationToken cancellationToken = default)
    {
        return SendStickerAsync(sticker, replyMarkup: null, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerStickerAsync(
        InputFileString sticker,
        ReplyMarkup replyMarkup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendStickerAsync(sticker, replyMarkup, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> ReplyStickerAsync(
        InputFileString sticker,
        CancellationToken cancellationToken = default)
    {
        return SendStickerAsync(sticker, replyMarkup: null, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyStickerAsync(
        InputFileString sticker,
        ReplyMarkup replyMarkup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendStickerAsync(sticker, replyMarkup, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> AnswerVideoNoteAsync(
        InputFileString videoNote,
        CancellationToken cancellationToken = default)
    {
        return SendVideoNoteAsync(videoNote, replyMarkup: null, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerVideoNoteAsync(
        InputFileString videoNote,
        ReplyMarkup replyMarkup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendVideoNoteAsync(videoNote, replyMarkup, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> ReplyVideoNoteAsync(
        InputFileString videoNote,
        CancellationToken cancellationToken = default)
    {
        return SendVideoNoteAsync(videoNote, replyMarkup: null, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyVideoNoteAsync(
        InputFileString videoNote,
        ReplyMarkup replyMarkup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendVideoNoteAsync(videoNote, replyMarkup, replyToCurrentMessage: true, cancellationToken);
    }

    private Task<Message> SendPhotoAsync(
        InputFileString photo,
        string? caption,
        TelegramParseMode? parseMode,
        ReplyMarkup? replyMarkup,
        bool replyToCurrentMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(photo);
        return _context.Bot.SendPhotoAsync(
            CurrentChatId,
            photo,
            caption: caption,
            parseMode: parseMode,
            replyParameters: CreateReplyParameters(replyToCurrentMessage),
            replyMarkup: replyMarkup,
            cancellationToken: ResolveCancellationToken(cancellationToken));
    }

    private Task<Message> SendDocumentAsync(
        InputFileString document,
        string? caption,
        TelegramParseMode? parseMode,
        ReplyMarkup? replyMarkup,
        bool replyToCurrentMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return _context.Bot.SendDocumentAsync(
            CurrentChatId,
            document,
            caption: caption,
            parseMode: parseMode,
            replyParameters: CreateReplyParameters(replyToCurrentMessage),
            replyMarkup: replyMarkup,
            cancellationToken: ResolveCancellationToken(cancellationToken));
    }

    private Task<Message> SendVideoAsync(
        InputFileString video,
        string? caption,
        TelegramParseMode? parseMode,
        ReplyMarkup? replyMarkup,
        bool replyToCurrentMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(video);
        return _context.Bot.SendVideoAsync(
            CurrentChatId,
            video,
            caption: caption,
            parseMode: parseMode,
            replyParameters: CreateReplyParameters(replyToCurrentMessage),
            replyMarkup: replyMarkup,
            cancellationToken: ResolveCancellationToken(cancellationToken));
    }

    private Task<Message> SendAnimationAsync(
        InputFileString animation,
        string? caption,
        TelegramParseMode? parseMode,
        ReplyMarkup? replyMarkup,
        bool replyToCurrentMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(animation);
        return _context.Bot.SendAnimationAsync(
            CurrentChatId,
            animation,
            caption: caption,
            parseMode: parseMode,
            replyParameters: CreateReplyParameters(replyToCurrentMessage),
            replyMarkup: replyMarkup,
            cancellationToken: ResolveCancellationToken(cancellationToken));
    }

    private Task<Message> SendAudioAsync(
        InputFileString audio,
        string? caption,
        TelegramParseMode? parseMode,
        ReplyMarkup? replyMarkup,
        bool replyToCurrentMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);
        return _context.Bot.SendAudioAsync(
            CurrentChatId,
            audio,
            caption: caption,
            parseMode: parseMode,
            replyParameters: CreateReplyParameters(replyToCurrentMessage),
            replyMarkup: replyMarkup,
            cancellationToken: ResolveCancellationToken(cancellationToken));
    }

    private Task<Message> SendVoiceAsync(
        InputFileString voice,
        string? caption,
        TelegramParseMode? parseMode,
        ReplyMarkup? replyMarkup,
        bool replyToCurrentMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(voice);
        return _context.Bot.SendVoiceAsync(
            CurrentChatId,
            voice,
            caption: caption,
            parseMode: parseMode,
            replyParameters: CreateReplyParameters(replyToCurrentMessage),
            replyMarkup: replyMarkup,
            cancellationToken: ResolveCancellationToken(cancellationToken));
    }

    private Task<Message> SendStickerAsync(
        InputFileString sticker,
        ReplyMarkup? replyMarkup,
        bool replyToCurrentMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sticker);
        return _context.Bot.SendStickerAsync(
            CurrentChatId,
            sticker,
            replyParameters: CreateReplyParameters(replyToCurrentMessage),
            replyMarkup: replyMarkup,
            cancellationToken: ResolveCancellationToken(cancellationToken));
    }

    private Task<Message> SendVideoNoteAsync(
        InputFileString videoNote,
        ReplyMarkup? replyMarkup,
        bool replyToCurrentMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(videoNote);
        return _context.Bot.SendVideoNoteAsync(
            CurrentChatId,
            videoNote,
            replyParameters: CreateReplyParameters(replyToCurrentMessage),
            replyMarkup: replyMarkup,
            cancellationToken: ResolveCancellationToken(cancellationToken));
    }

    private IntegerString CurrentChatId => IntegerString.From(_context.TelegramChat.Id);

    private ReplyParameters? CreateReplyParameters(bool replyToCurrentMessage)
    {
        return replyToCurrentMessage
            ? new ReplyParameters
            {
                MessageId = _context.TelegramMessage.MessageId
            }
            : null;
    }

}
