using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Types;

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

    public Task<Message> AnswerAsync(
        string text,
        InlineKeyboard keyboard,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        return AnswerAsync(
            text,
            ReplyMarkup.From(keyboard.ToMarkup(_context.CallbackData)),
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

    public Task<Message> ReplyAsync(
        string text,
        InlineKeyboard keyboard,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        return ReplyAsync(
            text,
            ReplyMarkup.From(keyboard.ToMarkup(_context.CallbackData)),
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

    [Obsolete("Use ReplyAsync instead.")]
    public Task<Message> ReplyTextAsync(string text, CancellationToken cancellationToken = default)
    {
        return ReplyAsync(text, cancellationToken);
    }

    [Obsolete("Use ReplyAsync instead.")]
    public Task<Message> ReplyTextAsync(
        string text,
        InlineKeyboard keyboard,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        return ReplyAsync(text, keyboard, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return _context.Bot.SendMessageAsync(
            IntegerString.From(_context.TelegramChat.Id),
            text,
            replyParameters: replyToCurrentMessage
                ? new ReplyParameters
                {
                    MessageId = _context.TelegramMessage.MessageId
                }
                : null,
            replyMarkup: replyMarkup,
            cancellationToken: ResolveCancellationToken(cancellationToken));
    }

    public Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
    {
        return _context.Bot.DeleteMessageAsync(
            IntegerString.From(_context.TelegramChat.Id),
            _context.TelegramMessage.MessageId,
            ResolveCancellationToken(cancellationToken));
    }

    private CancellationToken ResolveCancellationToken(CancellationToken cancellationToken)
    {
        return cancellationToken.CanBeCanceled ? cancellationToken : _context.CancellationToken;
    }
}
