namespace TeleFlow.Telegram.Webhooks;

public sealed class TelegramWebhookOptions
{
    public string Path { get; set; } = "/telegram/webhook";

    public string? SecretToken { get; set; }
}
