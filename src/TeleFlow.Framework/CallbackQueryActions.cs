using TeleFlow.Telegram.Formatting;
using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Types;

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
        return AnswerCoreAsync(text: null, showAlert: null, cancellationToken);
    }

    public Task<bool> AnswerAsync(string? text, CancellationToken cancellationToken)
    {
        return AnswerCoreAsync(text, showAlert: null, cancellationToken);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Callback action methods intentionally remain instance methods for a consistent fluent context API.")]
    public Task<bool> AnswerAsync(string? text, bool showAlert, CancellationToken cancellationToken)
    {
        return AnswerCoreAsync(text, showAlert, cancellationToken);
    }

    public Task<bool> AnswerAsync(
        string? text = null,
        bool? showAlert = null,
        CancellationToken cancellationToken = default)
    {
        return AnswerCoreAsync(text, showAlert, cancellationToken);
    }

    private async Task<bool> AnswerCoreAsync(
        string? text,
        bool? showAlert,
        CancellationToken cancellationToken)
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
        return EditTextCoreAsync(text, replyMarkup: null, parseMode: null, cancellationToken);
    }

    /// <summary>
    /// Edits the callback message with formatted text.
    /// The formatted value carries its explicit Telegram parse mode and does not use the client's parse-mode default.
    /// </summary>
    public Task<MessageBoolean> EditTextAsync(
        TelegramFormattedText text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        return EditTextCoreAsync(text.Text, replyMarkup: null, text.ParseMode, cancellationToken);
    }

    public Task<MessageBoolean> EditTextAsync(
        string text,
        InlineKeyboardMarkup replyMarkup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return EditTextCoreAsync(text, replyMarkup, parseMode: null, cancellationToken);
    }

    /// <summary>
    /// Edits the callback message with formatted text and an inline keyboard.
    /// </summary>
    public Task<MessageBoolean> EditTextAsync(
        TelegramFormattedText text,
        InlineKeyboardMarkup replyMarkup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return EditTextCoreAsync(text.Text, replyMarkup, text.ParseMode, cancellationToken);
    }

    private Task<MessageBoolean> EditTextCoreAsync(
        string text,
        InlineKeyboardMarkup? replyMarkup,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (CallbackQueryMessageTargetResolver.TryResolve(_context.TelegramCallbackQuery, out var target))
        {
            if (target.IsEphemeral)
            {
                return EditEphemeralTextAsync(target, text, replyMarkup, parseMode, cancellationToken);
            }

            return _context.Bot.EditMessageTextAsync(
                chatId: IntegerString.From(target.ChatId),
                messageId: target.MessageId,
                text: text,
                parseMode: parseMode,
                replyMarkup: replyMarkup,
                cancellationToken: ResolveCancellationToken(cancellationToken));
        }

        if (!string.IsNullOrWhiteSpace(_context.TelegramCallbackQuery.InlineMessageId))
        {
            return _context.Bot.EditMessageTextAsync(
                inlineMessageId: _context.TelegramCallbackQuery.InlineMessageId,
                text: text,
                parseMode: parseMode,
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

        if (target.IsEphemeral)
        {
            return _context.Bot.DeleteEphemeralMessageAsync(
                IntegerString.From(target.ChatId),
                EphemeralMessageTargetResolver.ResolveReceiverUserId(_context.TelegramCallbackQuery, target),
                target.EphemeralMessageId!.Value,
                ResolveCancellationToken(cancellationToken));
        }

        return _context.Bot.DeleteMessageAsync(
            IntegerString.From(target.ChatId),
            target.MessageId,
            ResolveCancellationToken(cancellationToken));
    }

    /// <summary>
    /// Sends a text message visible only to the user who triggered the callback in a group or supergroup chat.
    /// Sending the message does not acknowledge the callback query.
    /// </summary>
    public Task<EphemeralMessageReference> SendEphemeralAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return SendEphemeralCoreAsync(text, inlineKeyboard: null, cancellationToken);
    }

    /// <summary>
    /// Sends a text message with an inline keyboard visible only to the user who triggered the callback in a group or supergroup chat.
    /// Sending the message does not acknowledge the callback query.
    /// </summary>
    public Task<EphemeralMessageReference> SendEphemeralAsync(
        string text,
        InlineKeyboardMarkup replyMarkup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyMarkup);
        return SendEphemeralCoreAsync(text, replyMarkup, cancellationToken);
    }

    private async Task<MessageBoolean> EditEphemeralTextAsync(
        TelegramMessageTarget target,
        string text,
        InlineKeyboardMarkup? replyMarkup,
        TelegramParseMode? parseMode,
        CancellationToken cancellationToken)
    {
        var result = await _context.Bot.EditEphemeralMessageTextAsync(
            IntegerString.From(target.ChatId),
            EphemeralMessageTargetResolver.ResolveReceiverUserId(_context.TelegramCallbackQuery, target),
            target.EphemeralMessageId!.Value,
            text,
            parseMode: parseMode,
            replyMarkup: replyMarkup,
            cancellationToken: ResolveCancellationToken(cancellationToken)).ConfigureAwait(false);

        return MessageBoolean.From(result);
    }

    private Task<EphemeralMessageReference> SendEphemeralCoreAsync(
        string text,
        InlineKeyboardMarkup? inlineKeyboard,
        CancellationToken cancellationToken)
    {
        if (!CallbackQueryMessageTargetResolver.TryResolve(_context.TelegramCallbackQuery, out var target))
        {
            throw new InvalidOperationException(
                "The current callback query does not contain a group or supergroup chat target for an ephemeral message.");
        }

        var ephemeralTarget = EphemeralMessageTargetResolver.ResolveForCallback(
            _context.TelegramCallbackQuery,
            target);

        return _context.Bot.SendEphemeralMessageAsync(
            ephemeralTarget,
            text,
            replyMarkup: inlineKeyboard,
            callbackQueryId: _context.TelegramCallbackQuery.Id,
            cancellationToken: ResolveCancellationToken(cancellationToken));
    }

    private CancellationToken ResolveCancellationToken(CancellationToken cancellationToken)
    {
        return cancellationToken.CanBeCanceled ? cancellationToken : _context.CancellationToken;
    }
}
