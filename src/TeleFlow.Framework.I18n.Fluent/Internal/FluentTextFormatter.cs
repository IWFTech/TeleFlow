using System.Globalization;
using Linguini.Bundle;
using Linguini.Shared.Types.Bundle;
using TeleFlow.Telegram.Formatting;

namespace TeleFlow.Telegram.I18n.Fluent.Internal;

/// <summary>
/// Formats messages from immutable startup-loaded catalogs and enforces argument escaping and failure semantics.
/// </summary>
internal sealed class FluentTextFormatter(FluentCatalog catalog) : IFluentTextFormatter
{
    public string Format(
        Locale locale,
        string messageId,
        params ReadOnlySpan<I18nArgument> arguments)
    {
        return FormatCore(locale, messageId, arguments, FluentRenderingMode.Plain);
    }

    public TelegramFormattedText FormatHtml(
        Locale locale,
        string messageId,
        params ReadOnlySpan<I18nArgument> arguments)
    {
        var text = FormatCore(locale, messageId, arguments, FluentRenderingMode.Html);

        try
        {
            return TelegramHtml.UnsafeMarkup(text);
        }
        catch (ArgumentException exception)
        {
            throw new FluentLocalizationException(
                messageId,
                locale,
                "the formatted HTML message was empty",
                exception);
        }
    }

    public TelegramFormattedText FormatMarkdownV2(
        Locale locale,
        string messageId,
        params ReadOnlySpan<I18nArgument> arguments)
    {
        var text = FormatCore(locale, messageId, arguments, FluentRenderingMode.MarkdownV2);

        try
        {
            return TelegramMarkdownV2.UnsafeMarkup(text);
        }
        catch (ArgumentException exception)
        {
            throw new FluentLocalizationException(
                messageId,
                locale,
                "the formatted MarkdownV2 message was empty",
                exception);
        }
    }

    private string FormatCore(
        Locale locale,
        string messageId,
        ReadOnlySpan<I18nArgument> arguments,
        FluentRenderingMode mode)
    {
        ArgumentNullException.ThrowIfNull(locale);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        var (message, attribute) = ParseMessageId(messageId, locale);
        var resolved = catalog.Resolve(locale, mode);

        try
        {
            var fluentArguments = ConvertArguments(
                arguments,
                resolved.Locale.Culture,
                mode,
                messageId,
                locale);

            IReadBundle bundle = resolved.Bundle;

            if (!bundle.TryGetMessage(
                    message,
                    attribute,
                    fluentArguments,
                    out var errors,
                    out var formatted) ||
                formatted is null)
            {
                throw new FluentLocalizationException(
                    messageId,
                    locale,
                    errors is { Count: > 0 }
                        ? "the message, attribute, variable, or function could not be resolved"
                        : "the message or attribute was not found");
            }

            return formatted;
        }
        catch (FluentLocalizationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new FluentLocalizationException(
                messageId,
                locale,
                "an argument or Fluent function could not be formatted",
                exception);
        }
    }

    private static Dictionary<string, IFluentType>? ConvertArguments(
        ReadOnlySpan<I18nArgument> arguments,
        CultureInfo culture,
        FluentRenderingMode mode,
        string messageId,
        Locale requestedLocale)
    {
        if (arguments.IsEmpty)
        {
            return null;
        }

        var result = new Dictionary<string, IFluentType>(arguments.Length, StringComparer.Ordinal);

        foreach (var argument in arguments)
        {
            if (!result.TryAdd(
                    argument.Name,
                    FluentValueConverter.Convert(argument.Value, culture, mode)))
            {
                throw new FluentLocalizationException(
                    messageId,
                    requestedLocale,
                    $"argument '{argument.Name}' was provided more than once");
            }
        }

        return result;
    }

    private static (string Message, string? Attribute) ParseMessageId(
        string messageId,
        Locale locale)
    {
        var firstDot = messageId.IndexOf('.');

        if (firstDot < 0)
        {
            return (messageId, null);
        }

        if (firstDot == 0 ||
            firstDot == messageId.Length - 1 ||
            messageId.IndexOf('.', firstDot + 1) >= 0)
        {
            throw new FluentLocalizationException(
                messageId,
                locale,
                "message identifiers must use 'message-id' or 'message-id.attribute'");
        }

        return (messageId[..firstDot], messageId[(firstDot + 1)..]);
    }
}
