namespace TeleFlow.Telegram.I18n.Fluent;

/// <summary>
/// Represents a Fluent lookup, argument, function, or formatting failure for one message and locale.
/// Diagnostic text deliberately excludes argument values and translated message contents.
/// </summary>
public sealed class FluentLocalizationException : InvalidOperationException
{
    public FluentLocalizationException(string messageId, Locale locale, string reason)
        : base(CreateMessage(messageId, locale, reason))
    {
        MessageId = messageId;
        Locale = locale;
    }

    public FluentLocalizationException(
        string messageId,
        Locale locale,
        string reason,
        Exception innerException)
        : base(CreateMessage(messageId, locale, reason), innerException)
    {
        MessageId = messageId;
        Locale = locale;
    }

    /// <summary>
    /// Gets the requested Fluent message or message-attribute identifier.
    /// </summary>
    public string MessageId { get; }

    /// <summary>
    /// Gets the requested locale before exact, parent, and fallback catalog selection.
    /// </summary>
    public Locale Locale { get; }

    private static string CreateMessage(string messageId, Locale locale, string reason)
    {
        return $"Fluent message '{messageId}' could not be formatted for locale '{locale.Name}': {reason}";
    }
}
