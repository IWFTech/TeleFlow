using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Types;
using TeleFlow.Telegram.Formatting;

namespace TeleFlow.Telegram;

public sealed partial class MessageActions
{
    private readonly MessageContext _context;

    internal MessageActions(MessageContext context)
    {
        _context = context;
    }

    public Task<Message> AnswerAsync(string text, CancellationToken cancellationToken = default)
    {
        return SendTextAsync(text, replyMarkup: null, replyToCurrentMessage: false, cancellationToken);
    }

    /// <summary>
    /// Sends formatted text to the current chat without replying to the current message.
    /// The formatted value carries its explicit Telegram parse mode and does not use the client's parse-mode default.
    /// </summary>
    public Task<Message> AnswerAsync(
        TelegramFormattedText text,
        CancellationToken cancellationToken = default)
    {
        return SendFormattedTextAsync(text, replyMarkup: null, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> AnswerAsync(
        string text,
        InlineKeyboardMarkup inlineKeyboard,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inlineKeyboard);
        return AnswerAsync(
            text,
            ReplyMarkup.From(inlineKeyboard),
            cancellationToken);
    }

    /// <summary>
    /// Sends formatted text with an inline keyboard to the current chat without replying to the current message.
    /// </summary>
    public Task<Message> AnswerAsync(
        TelegramFormattedText text,
        InlineKeyboardMarkup inlineKeyboard,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inlineKeyboard);
        return SendFormattedTextAsync(
            text,
            ReplyMarkup.From(inlineKeyboard),
            replyToCurrentMessage: false,
            cancellationToken);
    }

    public Task<Message> AnswerAsync(
        string text,
        ReplyKeyboard keyboard,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        return AnswerAsync(
            text,
            ReplyMarkup.From(keyboard.ToMarkup()),
            cancellationToken);
    }

    public Task<Message> AnswerAsync(
        string text,
        KeyboardRemove keyboardRemove,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyboardRemove);
        return AnswerAsync(
            text,
            ReplyMarkup.From(keyboardRemove.ToMarkup()),
            cancellationToken);
    }

    public Task<Message> AnswerAsync(
        string text,
        ForceReplyBuilder forceReply,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(forceReply);
        return AnswerAsync(
            text,
            ReplyMarkup.From(forceReply.ToMarkup()),
            cancellationToken);
    }

    public Task<Message> AnswerAsync(
        string text,
        ReplyMarkup replyMarkup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendTextAsync(text, replyMarkup, replyToCurrentMessage: false, cancellationToken);
    }

    public Task<Message> ReplyAsync(string text, CancellationToken cancellationToken = default)
    {
        return SendTextAsync(text, replyMarkup: null, replyToCurrentMessage: true, cancellationToken);
    }

    /// <summary>
    /// Sends formatted text as a reply to the current message.
    /// The formatted value carries its explicit Telegram parse mode and does not use the client's parse-mode default.
    /// </summary>
    public Task<Message> ReplyAsync(
        TelegramFormattedText text,
        CancellationToken cancellationToken = default)
    {
        return SendFormattedTextAsync(text, replyMarkup: null, replyToCurrentMessage: true, cancellationToken);
    }

    public Task<Message> ReplyAsync(
        string text,
        InlineKeyboardMarkup inlineKeyboard,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inlineKeyboard);
        return ReplyAsync(
            text,
            ReplyMarkup.From(inlineKeyboard),
            cancellationToken);
    }

    /// <summary>
    /// Sends formatted text with an inline keyboard as a reply to the current message.
    /// </summary>
    public Task<Message> ReplyAsync(
        TelegramFormattedText text,
        InlineKeyboardMarkup inlineKeyboard,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inlineKeyboard);
        return SendFormattedTextAsync(
            text,
            ReplyMarkup.From(inlineKeyboard),
            replyToCurrentMessage: true,
            cancellationToken);
    }

    public Task<Message> ReplyAsync(
        string text,
        ReplyKeyboard keyboard,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        return ReplyAsync(
            text,
            ReplyMarkup.From(keyboard.ToMarkup()),
            cancellationToken);
    }

    public Task<Message> ReplyAsync(
        string text,
        KeyboardRemove keyboardRemove,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyboardRemove);
        return ReplyAsync(
            text,
            ReplyMarkup.From(keyboardRemove.ToMarkup()),
            cancellationToken);
    }

    public Task<Message> ReplyAsync(
        string text,
        ForceReplyBuilder forceReply,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(forceReply);
        return ReplyAsync(
            text,
            ReplyMarkup.From(forceReply.ToMarkup()),
            cancellationToken);
    }

    public Task<Message> ReplyAsync(
        string text,
        ReplyMarkup replyMarkup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendTextAsync(text, replyMarkup, replyToCurrentMessage: true, cancellationToken);
    }

    /// <summary>
    /// Sends a text message visible only to the user represented by the current group or supergroup update.
    /// </summary>
    public Task<EphemeralMessageReference> SendEphemeralAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return SendEphemeralCoreAsync(text, inlineKeyboard: null, cancellationToken);
    }

    /// <summary>
    /// Sends a text message with an inline keyboard visible only to the user represented by the current group or supergroup update.
    /// </summary>
    public Task<EphemeralMessageReference> SendEphemeralAsync(
        string text,
        InlineKeyboardMarkup replyMarkup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendEphemeralCoreAsync(text, replyMarkup, cancellationToken);
    }

    [Obsolete("Use ReplyAsync instead.")]
    public Task<Message> ReplyTextAsync(string text, CancellationToken cancellationToken = default)
    {
        return ReplyAsync(text, cancellationToken);
    }

    [Obsolete("Use ReplyAsync instead.")]
    public Task<Message> ReplyTextAsync(
        string text,
        InlineKeyboardMarkup inlineKeyboard,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inlineKeyboard);
        return ReplyAsync(text, inlineKeyboard, cancellationToken);
    }

    [Obsolete("Use ReplyAsync instead.")]
    public Task<Message> ReplyTextAsync(
        string text,
        ReplyMarkup replyMarkup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return ReplyAsync(text, replyMarkup, cancellationToken);
    }

    private Task<Message> SendTextAsync(
        string text,
        ReplyMarkup? replyMarkup,
        bool replyToCurrentMessage,
        CancellationToken cancellationToken,
        TelegramParseMode? parseMode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var reply = CreateReplyConfiguration(replyToCurrentMessage);

        return _context.Bot.SendMessageAsync(
            IntegerString.From(_context.TelegramChat.Id),
            text,
            receiverUserId: reply.ReceiverUserId,
            replyParameters: reply.ReplyParameters,
            replyMarkup: replyMarkup,
            parseMode: parseMode,
            cancellationToken: ResolveCancellationToken(cancellationToken));
    }

    private Task<Message> SendFormattedTextAsync(
        TelegramFormattedText text,
        ReplyMarkup? replyMarkup,
        bool replyToCurrentMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        return SendTextAsync(
            text.Text,
            replyMarkup,
            replyToCurrentMessage,
            cancellationToken,
            text.ParseMode);
    }

    public Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (_context.TelegramMessage.EphemeralMessageId is long ephemeralMessageId)
        {
            var target = EphemeralMessageTargetResolver.ResolveForEphemeralMessage(_context.TelegramMessage);
            return _context.Bot.DeleteEphemeralMessageAsync(
                target.ChatId,
                target.ReceiverUserId,
                ephemeralMessageId,
                ResolveCancellationToken(cancellationToken));
        }

        return _context.Bot.DeleteMessageAsync(
            IntegerString.From(_context.TelegramChat.Id),
            _context.TelegramMessage.MessageId,
            ResolveCancellationToken(cancellationToken));
    }

    private Task<EphemeralMessageReference> SendEphemeralCoreAsync(
        string text,
        InlineKeyboardMarkup? inlineKeyboard,
        CancellationToken cancellationToken)
    {
        var target = EphemeralMessageTargetResolver.ResolveForMessage(_context.TelegramMessage);
        return _context.Bot.SendEphemeralMessageAsync(
            target,
            text,
            replyMarkup: inlineKeyboard,
            cancellationToken: ResolveCancellationToken(cancellationToken));
    }

    private (ReplyParameters? ReplyParameters, long? ReceiverUserId) CreateReplyConfiguration(
        bool replyToCurrentMessage)
    {
        if (!replyToCurrentMessage)
        {
            return default;
        }

        if (_context.TelegramMessage.EphemeralMessageId is long ephemeralMessageId)
        {
            var target = EphemeralMessageTargetResolver.ResolveForEphemeralMessage(_context.TelegramMessage);
            return (
                new ReplyParameters { EphemeralMessageId = ephemeralMessageId },
                target.ReceiverUserId);
        }

        return (new ReplyParameters { MessageId = _context.TelegramMessage.MessageId }, null);
    }

    private CancellationToken ResolveCancellationToken(CancellationToken cancellationToken)
    {
        return cancellationToken.CanBeCanceled ? cancellationToken : _context.CancellationToken;
    }
}
