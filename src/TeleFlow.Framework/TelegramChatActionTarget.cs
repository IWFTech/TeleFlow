namespace TeleFlow.Telegram;

internal readonly record struct TelegramChatActionTarget(
    long ChatId,
    string? BusinessConnectionId,
    long? MessageThreadId);
