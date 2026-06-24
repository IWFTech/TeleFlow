namespace TeleFlow.Telegram;

public sealed record TelegramTransportRequest(
    string MethodName,
    Uri Uri,
    TelegramTransportContent Content);
