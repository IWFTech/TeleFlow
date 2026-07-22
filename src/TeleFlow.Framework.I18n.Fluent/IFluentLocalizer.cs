using TeleFlow.Telegram.Formatting;

namespace TeleFlow.Telegram.I18n.Fluent;

/// <summary>
/// Formats startup-loaded Fluent messages for the locale resolved in the current Telegram update scope.
/// </summary>
public interface IFluentLocalizer
{
    /// <summary>
    /// Formats a plain-text message for the current update locale.
    /// </summary>
    string Format(string messageId, params ReadOnlySpan<I18nArgument> arguments);

    /// <summary>
    /// Formats trusted resource HTML with escaped dynamic arguments for the current update locale.
    /// </summary>
    TelegramFormattedText FormatHtml(
        string messageId,
        params ReadOnlySpan<I18nArgument> arguments);

    /// <summary>
    /// Formats trusted resource MarkdownV2 with escaped dynamic arguments for the current update locale.
    /// </summary>
    TelegramFormattedText FormatMarkdownV2(
        string messageId,
        params ReadOnlySpan<I18nArgument> arguments);
}
