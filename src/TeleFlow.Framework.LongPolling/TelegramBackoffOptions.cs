namespace TeleFlow.Telegram;

public sealed class TelegramBackoffOptions
{
    public bool Enabled { get; set; } = true;

    public TimeSpan MinDelay { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(5);

    public double Factor { get; set; } = 1.3;

    public double Jitter { get; set; } = 0.1;
}
