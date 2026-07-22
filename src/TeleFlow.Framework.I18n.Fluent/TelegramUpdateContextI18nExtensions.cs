using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Telegram.Formatting;

namespace TeleFlow.Telegram.I18n.Fluent;

/// <summary>
/// Provides concise presentation-layer Fluent formatting from Telegram handler contexts without performing I/O.
/// </summary>
public static class TelegramUpdateContextI18nExtensions
{
    /// <summary>
    /// Formats a plain-text Fluent message for the current update locale.
    /// </summary>
    public static string I18n(
        this TelegramUpdateContext context,
        string messageId,
        params ReadOnlySpan<I18nArgument> arguments)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Services.GetRequiredService<IFluentLocalizer>().Format(messageId, arguments);
    }

    /// <summary>
    /// Formats a Telegram HTML Fluent message for the current update locale.
    /// </summary>
    public static TelegramFormattedText I18nHtml(
        this TelegramUpdateContext context,
        string messageId,
        params ReadOnlySpan<I18nArgument> arguments)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Services.GetRequiredService<IFluentLocalizer>().FormatHtml(messageId, arguments);
    }

    /// <summary>
    /// Formats a Telegram MarkdownV2 Fluent message for the current update locale.
    /// </summary>
    public static TelegramFormattedText I18nMarkdownV2(
        this TelegramUpdateContext context,
        string messageId,
        params ReadOnlySpan<I18nArgument> arguments)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Services.GetRequiredService<IFluentLocalizer>().FormatMarkdownV2(messageId, arguments);
    }
}
