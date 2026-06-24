namespace TeleFlow.Telegram;

public sealed class TelegramRawLongPollingOptions
{
    public int TimeoutSeconds { get; set; } = 30;

    public int Limit { get; set; } = 100;

    public IReadOnlyList<string>? AllowedUpdates { get; set; }

    public TelegramRawLongPollingBackoffOptions Backoff { get; set; } = new();
}
