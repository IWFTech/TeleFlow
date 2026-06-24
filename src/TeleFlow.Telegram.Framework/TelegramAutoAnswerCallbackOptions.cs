namespace TeleFlow.Telegram;

public sealed class TelegramAutoAnswerCallbackOptions
{
    public bool Enabled { get; set; } = true;

    public string? Text { get; set; }

    public bool ShowAlert { get; set; }
}
