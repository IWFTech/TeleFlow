namespace TeleFlow.Telegram;

public sealed class TelegramBotOptions
{
    public string Token { get; set; } = string.Empty;

    public string? BotUsername { get; set; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1056:URI-like properties should not be strings",
        Justification = "Options use a string BaseUrl to preserve simple configuration binding from JSON, environment variables, and user secrets.")]
    public string BaseUrl { get; set; } = "https://api.telegram.org";

    public TelegramBotDefaults Defaults { get; set; } = new();

    public TelegramRoleFilterOptions RoleFilter { get; set; } = new();

    /// <summary>
    /// Gets or sets the bounded retry policy forwarded to the low-level Telegram client.
    /// </summary>
    public TelegramRetryAfterPolicy RetryAfter { get; set; } = TelegramRetryAfterPolicy.Default;
}
