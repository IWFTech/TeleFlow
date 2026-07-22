namespace TeleFlow.Telegram.I18n.Fluent;

/// <summary>
/// Configures the startup-loaded Fluent catalog used by Telegram handlers and explicit-locale background formatting.
/// </summary>
public sealed class TelegramFluentI18nOptions
{
    /// <summary>
    /// Gets or sets the resource root. Relative paths resolve from <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    public string ResourcesPath { get; set; } = "Locales";

    /// <summary>
    /// Gets or sets the final locale used when exact and parent catalogs are unavailable.
    /// Its catalog must exist at startup.
    /// </summary>
    public Locale FallbackLocale { get; set; } = new("en");
}
