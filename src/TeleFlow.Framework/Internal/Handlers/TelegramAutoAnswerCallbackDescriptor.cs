namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed record TelegramAutoAnswerCallbackDescriptor(
    bool Enabled,
    string? Text,
    bool ShowAlert);
