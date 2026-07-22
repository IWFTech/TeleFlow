using TeleFlow.Telegram.Formatting;

namespace TeleFlow.Telegram.I18n.Fluent.Internal;

/// <summary>
/// Binds the explicit-locale formatter to the locale resolved once for the current Telegram update scope.
/// </summary>
internal sealed class FluentLocalizer(
    ILocaleAccessor localeAccessor,
    IFluentTextFormatter formatter) : IFluentLocalizer
{
    public string Format(string messageId, params ReadOnlySpan<I18nArgument> arguments)
    {
        return formatter.Format(localeAccessor.Current, messageId, arguments);
    }

    public TelegramFormattedText FormatHtml(
        string messageId,
        params ReadOnlySpan<I18nArgument> arguments)
    {
        return formatter.FormatHtml(localeAccessor.Current, messageId, arguments);
    }

    public TelegramFormattedText FormatMarkdownV2(
        string messageId,
        params ReadOnlySpan<I18nArgument> arguments)
    {
        return formatter.FormatMarkdownV2(localeAccessor.Current, messageId, arguments);
    }
}
