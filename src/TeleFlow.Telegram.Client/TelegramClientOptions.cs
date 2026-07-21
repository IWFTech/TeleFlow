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

    /// <summary>
    /// Gets or sets the Telegram Bot API environment used to build outgoing method URIs.
    /// </summary>
    public TelegramBotApiEnvironment Environment { get; set; } = TelegramBotApiEnvironment.Production;

    public TelegramBotDefaults Defaults { get; set; } = new();

    /// <summary>
    /// Gets or sets the bounded retry policy used when Telegram returns Bot API throttling metadata.
    /// </summary>
    public TelegramRetryAfterPolicy RetryAfter { get; set; } = TelegramRetryAfterPolicy.Default;
}
