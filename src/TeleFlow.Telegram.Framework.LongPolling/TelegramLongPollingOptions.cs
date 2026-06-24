namespace TeleFlow.Telegram;

public sealed class TelegramLongPollingOptions
{
    public int TimeoutSeconds { get; set; } = 30;

    public int Limit { get; set; } = 100;

    public TelegramAllowedUpdates AllowedUpdates { get; set; } = TelegramAllowedUpdates.Auto;

    public TelegramBackoffOptions Backoff { get; set; } = new();
}
