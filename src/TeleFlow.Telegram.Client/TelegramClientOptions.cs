using System.Diagnostics.CodeAnalysis;

namespace TeleFlow.Telegram;

public sealed class TelegramClientOptions
{
    public string Token { get; set; } = string.Empty;

    public string? BotUsername { get; set; }

    [SuppressMessage(
        "Design",
        "CA1056:URI-like properties should not be strings",
        Justification = "Client options must bind naturally from JSON, environment variables, and user secrets.")]
    public string BaseUrl { get; set; } = "https://api.telegram.org";

    public TelegramBotDefaults Defaults { get; set; } = new();
}
