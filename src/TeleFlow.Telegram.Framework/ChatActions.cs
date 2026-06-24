using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public sealed class ChatActions
{
    private static readonly TimeSpan DefaultRepeatDelay = TimeSpan.FromSeconds(4);

    private readonly TelegramUpdateContext _context;

    internal ChatActions(TelegramUpdateContext context)
    {
        _context = context;
    }

    public async ValueTask<ChatActionLease> ActionAsync(
        ChatAction action,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveTarget(out var target))
        {
            throw new InvalidOperationException(
                "The current Telegram update does not expose a Telegram chat target for chat actions.");
        }

        var effectiveToken = ResolveCancellationToken(cancellationToken);
        await SendChatActionAsync(_context.Bot, target, action, effectiveToken).ConfigureAwait(false);

        var timeProvider = _context.Services.GetService<TimeProvider>() ?? TimeProvider.System;
        return new ChatActionLease(
            _context.Bot,
            target,
            action,
            timeProvider,
            DefaultRepeatDelay,
            effectiveToken);
    }

    internal static Task<bool> SendChatActionAsync(
        ITelegramClient bot,
        TelegramChatActionTarget target,
        ChatAction action,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action.Value, nameof(action));

        return bot.SendChatActionAsync(
            IntegerString.From(target.ChatId),
            action.Value,
            businessConnectionId: target.BusinessConnectionId,
            messageThreadId: target.MessageThreadId,
            cancellationToken: cancellationToken);
    }

    private bool TryResolveTarget(out TelegramChatActionTarget target)
    {
        if (_context is MessageContext messageContext)
        {
            target = CreateMessageTarget(messageContext.TelegramMessage);
            return true;
        }

        if (_context is CallbackQueryContext callbackContext)
        {
            return TryResolveCallbackTarget(callbackContext.TelegramCallbackQuery, out target);
        }

        if (_context is ChatMemberUpdatedContext chatMemberContext)
        {
            target = new TelegramChatActionTarget(
                chatMemberContext.TelegramChat.Id,
                BusinessConnectionId: null,
                MessageThreadId: null);
            return true;
        }

        if (_context.Update.Message is { } message)
        {
            target = CreateMessageTarget(message);
            return true;
        }

        if (_context.Update.CallbackQuery is { } callbackQuery)
        {
            return TryResolveCallbackTarget(callbackQuery, out target);
        }

        var chatMemberUpdated = _context.Update.ChatMember ?? _context.Update.MyChatMember;
        if (chatMemberUpdated is not null)
        {
            target = new TelegramChatActionTarget(
                chatMemberUpdated.Chat.Id,
                BusinessConnectionId: null,
                MessageThreadId: null);
            return true;
        }

        target = default;
        return false;
    }

    private static TelegramChatActionTarget CreateMessageTarget(Message message)
    {
        return new TelegramChatActionTarget(
            message.Chat.Id,
            message.BusinessConnectionId,
            message.MessageThreadId);
    }

    private static bool TryResolveCallbackTarget(
        CallbackQuery callbackQuery,
        out TelegramChatActionTarget target)
    {
        if (callbackQuery.Message is null)
        {
            target = default;
            return false;
        }

        if (callbackQuery.Message.TryGetMessage(out var accessibleMessage) &&
            accessibleMessage is not null)
        {
            target = CreateMessageTarget(accessibleMessage);
            return true;
        }

        if (callbackQuery.Message.TryGetInaccessibleMessage(out var inaccessibleMessage) &&
            inaccessibleMessage is not null)
        {
            target = new TelegramChatActionTarget(
                inaccessibleMessage.Chat.Id,
                BusinessConnectionId: null,
                MessageThreadId: null);
            return true;
        }

        target = default;
        return false;
    }

    private CancellationToken ResolveCancellationToken(CancellationToken cancellationToken)
    {
        return cancellationToken.CanBeCanceled ? cancellationToken : _context.CancellationToken;
    }
}
