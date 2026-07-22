namespace TeleFlow.Telegram.Formatting;

/// <summary>
/// Creates safe MarkdownV2-formatted Telegram text for normal Bot API messages.
/// Plain values passed to the returned builder are escaped automatically.
/// </summary>
public static class TelegramMarkdownV2
{
    /// <summary>
    /// Creates a builder that renders Telegram-compatible MarkdownV2.
    /// </summary>
    public static TelegramTextBuilder Create()
    {
        return new TelegramTextBuilder(TelegramMarkdownV2TextRenderer.Instance);
    }

    /// <summary>
    /// Escapes plain dynamic text for safe insertion into reviewed Telegram MarkdownV2 markup.
    /// </summary>
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TelegramMarkdownV2TextRenderer.Instance.EscapeText(text);
    }

    /// <summary>
    /// Creates a MarkdownV2 formatted-text value from reviewed static markup.
    /// This bypasses escaping and structural validation and must never receive user-controlled input.
    /// </summary>
    public static TelegramFormattedText UnsafeMarkup(string markup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(markup);
        return new TelegramFormattedText(markup, TelegramParseMode.MarkdownV2);
    }
}
