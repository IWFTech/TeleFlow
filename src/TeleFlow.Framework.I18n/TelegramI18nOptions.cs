namespace TeleFlow.Telegram.I18n;

/// <summary>
/// Configures engine-neutral Telegram locale resolution before the update reaches application middleware and handlers.
/// </summary>
public sealed class TelegramI18nOptions
{
    /// <summary>
    /// Gets or sets the locale used when custom resolvers and Telegram user metadata provide no valid decision.
    /// </summary>
    public Locale FallbackLocale { get; set; } = new("en");
}
