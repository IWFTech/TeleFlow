namespace TeleFlow.Telegram.Formatting;

/// <summary>
/// Creates safe HTML-formatted Telegram text for normal Bot API messages.
/// Plain values passed to the returned builder are escaped automatically.
/// </summary>
public static class TelegramHtml
{
    /// <summary>
    /// Creates a builder that renders Telegram-compatible HTML.
    /// </summary>
    public static TelegramTextBuilder Create()
    {
        return new TelegramTextBuilder(TelegramHtmlTextRenderer.Instance);
    }

    /// <summary>
    /// Creates an HTML formatted-text value from reviewed static markup.
    /// This bypasses escaping and structural validation and must never receive user-controlled input.
    /// </summary>
    public static TelegramFormattedText UnsafeMarkup(string markup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(markup);
        return new TelegramFormattedText(markup, TelegramParseMode.Html);
    }
}
