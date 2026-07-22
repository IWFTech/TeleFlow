using TeleFlow.Telegram.Formatting;

namespace TeleFlow.Telegram.I18n.Fluent;

/// <summary>
/// Formats startup-loaded Fluent messages for an explicit locale outside Telegram update processing.
/// Background services, broadcasts, startup tasks, and outbox dispatchers use this contract.
/// </summary>
public interface IFluentTextFormatter
{
    /// <summary>
    /// Formats a plain-text message for an explicit locale.
    /// </summary>
    string Format(
        Locale locale,
        string messageId,
        params ReadOnlySpan<I18nArgument> arguments);

    /// <summary>
    /// Formats trusted resource HTML with escaped dynamic arguments for an explicit locale.
    /// </summary>
    TelegramFormattedText FormatHtml(
        Locale locale,
        string messageId,
        params ReadOnlySpan<I18nArgument> arguments);

    /// <summary>
    /// Formats trusted resource MarkdownV2 with escaped dynamic arguments for an explicit locale.
    /// </summary>
    TelegramFormattedText FormatMarkdownV2(
        Locale locale,
        string messageId,
        params ReadOnlySpan<I18nArgument> arguments);
}
