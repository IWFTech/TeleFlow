using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Schema.Abstractions;

namespace TeleFlow.Telegram;

public sealed class CallbackQueryActions
{
    private readonly CallbackQueryContext _context;

    internal CallbackQueryActions(CallbackQueryContext context)
    {
        _context = context;
    }

    public Task<bool> AnswerAsync(CancellationToken cancellationToken)
    {
        return AnswerAsync(text: null, showAlert: null, cancellationToken);
    }

    public Task<bool> AnswerAsync(string? text, CancellationToken cancellationToken)
    {
        return AnswerAsync(text, showAlert: null, cancellationToken);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Callback action methods intentionally remain instance methods for a consistent fluent context API.")]
    public Task<bool> AnswerAsync(string? text, bool showAlert, CancellationToken cancellationToken)
    {
        return AnswerAsync(text, showAlert: showAlert, cancellationToken);
    }

    public async Task<bool> AnswerAsync(
        string? text = null,
        bool? showAlert = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _context.Bot.AnswerCallbackQueryAsync(
            _context.TelegramCallbackQuery.Id,
            text,
            showAlert,
            cancellationToken: ResolveCancellationToken(cancellationToken)).ConfigureAwait(false);
        _context.MarkCallbackQueryAnswered();
        return result;
    }

    public Task<MessageBoolean> EditTextAsync(string text, CancellationToken cancellationToken = default)
    {
        return EditTextAsync(text, replyMarkup: null, cancellationToken);
    }

    public Task<MessageBoolean> EditTextAsync(
        string text,
        InlineKeyboard keyboard,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        return EditTextAsync(text, keyboard.ToMarkup(_context.CallbackData), cancellationToken);
    }

    private Task<MessageBoolean> EditTextAsync(
        string text,
        TeleFlow.Telegram.Schema.Types.InlineKeyboardMarkup? replyMarkup,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (CallbackQueryMessageTargetResolver.TryResolve(_context.TelegramCallbackQuery, out var target))
        {
            return _context.Bot.EditMessageTextAsync(
                chatId: IntegerString.From(target.ChatId),
                messageId: target.MessageId,
                text: text,
                replyMarkup: replyMarkup,
                cancellationToken: ResolveCancellationToken(cancellationToken));
        }

        if (!string.IsNullOrWhiteSpace(_context.TelegramCallbackQuery.InlineMessageId))
        {
            return _context.Bot.EditMessageTextAsync(
                inlineMessageId: _context.TelegramCallbackQuery.InlineMessageId,
                text: text,
                replyMarkup: replyMarkup,
                cancellationToken: ResolveCancellationToken(cancellationToken));
        }

        throw new InvalidOperationException(
            "The current callback query does not contain a chat message target or an inline message identifier.");
    }

    public Task<bool> DeleteMessageAsync(CancellationToken cancellationToken = default)
    {
        if (!CallbackQueryMessageTargetResolver.TryResolve(_context.TelegramCallbackQuery, out var target))
        {
            throw new InvalidOperationException(
                "The current callback query does not contain a deletable chat message target.");
        }

        return _context.Bot.DeleteMessageAsync(
            IntegerString.From(target.ChatId),
            target.MessageId,
            ResolveCancellationToken(cancellationToken));
    }

    private CancellationToken ResolveCancellationToken(CancellationToken cancellationToken)
    {
        return cancellationToken.CanBeCanceled ? cancellationToken : _context.CancellationToken;
    }
}
