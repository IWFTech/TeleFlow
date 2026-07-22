using System.Globalization;
using Linguini.Shared.Types.Bundle;
using TeleFlow.Telegram.Formatting;

namespace TeleFlow.Telegram.I18n.Fluent.Internal;

/// <summary>
/// Converts application arguments into Linguini values while applying the selected Telegram escaping boundary exactly once.
/// </summary>
internal static class FluentValueConverter
{
    public static IFluentType Convert(
        object? value,
        CultureInfo culture,
        FluentRenderingMode mode)
    {
        return value switch
        {
            null => (FluentString)string.Empty,
            TelegramFormattedText formattedText => ConvertFormattedText(formattedText, mode),
            DateTime dateTime => new FluentDateTimeValue(dateTime, culture, mode),
            DateTimeOffset dateTimeOffset => new FluentDateTimeValue(dateTimeOffset, culture, mode),
            DateOnly date => new FluentDateTimeValue(date, culture, mode),
            TimeOnly time => new FluentDateTimeValue(time, culture, mode),
            byte number => (FluentNumber)(double)number,
            sbyte number => (FluentNumber)(double)number,
            short number => (FluentNumber)(double)number,
            ushort number => (FluentNumber)(double)number,
            int number => (FluentNumber)(double)number,
            uint number => (FluentNumber)(double)number,
            long number => (FluentNumber)number,
            ulong number => (FluentNumber)number,
            float number => (FluentNumber)number,
            double number => (FluentNumber)number,
            decimal number => (FluentNumber)(double)number,
            string text => (FluentString)Escape(text, mode),
            char character => (FluentString)Escape(character.ToString(), mode),
            IFormattable formattable => (FluentString)Escape(formattable.ToString(null, culture) ?? string.Empty, mode),
            _ => (FluentString)Escape(value.ToString() ?? string.Empty, mode)
        };
    }

    public static string Escape(string value, FluentRenderingMode mode)
    {
        return mode switch
        {
            FluentRenderingMode.Plain => value,
            FluentRenderingMode.Html => TelegramHtml.Escape(value),
            FluentRenderingMode.MarkdownV2 => TelegramMarkdownV2.Escape(value),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown Fluent rendering mode.")
        };
    }

    private static IFluentType ConvertFormattedText(
        TelegramFormattedText formattedText,
        FluentRenderingMode mode)
    {
        var expectedParseMode = mode switch
        {
            FluentRenderingMode.Html => TelegramParseMode.Html,
            FluentRenderingMode.MarkdownV2 => TelegramParseMode.MarkdownV2,
            FluentRenderingMode.Plain => throw new InvalidOperationException(
                "TelegramFormattedText cannot be inserted into a plain Fluent message."),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown Fluent rendering mode.")
        };

        if (formattedText.ParseMode != expectedParseMode)
        {
            throw new InvalidOperationException(
                $"TelegramFormattedText with parse mode '{formattedText.ParseMode}' cannot be inserted into '{expectedParseMode}' Fluent output.");
        }

        return new TrustedFormattedFluentValue(formattedText.Text);
    }
}

/// <summary>
/// Carries a reviewed formatted Telegram fragment through Fluent placeable resolution without escaping it again.
/// </summary>
internal sealed class TrustedFormattedFluentValue(string text) : IFluentType
{
    private readonly string _text = text;

    public string AsString() => _text;

    public bool IsError() => false;

    public bool Matches(IFluentType other, IScope scope)
    {
        ArgumentNullException.ThrowIfNull(other);
        ArgumentNullException.ThrowIfNull(scope);
        return other is TrustedFormattedFluentValue formatted &&
               string.Equals(_text, formatted._text, StringComparison.Ordinal);
    }

    public IFluentType Copy() => this;
}

/// <summary>
/// Carries a .NET temporal value through Fluent so DATETIME can apply options and direct placeables remain locale-aware and escaped.
/// </summary>
internal sealed class FluentDateTimeValue(
    IFormattable value,
    CultureInfo culture,
    FluentRenderingMode mode) : IFluentType
{
    private readonly IFormattable _value = value;

    public string AsString()
    {
        return FluentValueConverter.Escape(Format(culture, null, null), mode);
    }

    public bool IsError() => false;

    public bool Matches(IFluentType other, IScope scope)
    {
        ArgumentNullException.ThrowIfNull(other);
        ArgumentNullException.ThrowIfNull(scope);
        return other is FluentDateTimeValue dateTime && Equals(_value, dateTime._value);
    }

    public IFluentType Copy() => this;

    public string Format(CultureInfo targetCulture, string? dateStyle, string? timeStyle)
    {
        var normalizedDateStyle = NormalizeStyle(dateStyle, "dateStyle");
        var normalizedTimeStyle = NormalizeStyle(timeStyle, "timeStyle");

        if (_value is DateOnly date)
        {
            if (normalizedTimeStyle is not null)
            {
                throw new InvalidOperationException("DATETIME cannot apply timeStyle to a DateOnly value.");
            }

            return date.ToString(normalizedDateStyle == "long" ? "D" : "d", targetCulture);
        }

        if (_value is TimeOnly time)
        {
            if (normalizedDateStyle is not null)
            {
                throw new InvalidOperationException("DATETIME cannot apply dateStyle to a TimeOnly value.");
            }

            return time.ToString(normalizedTimeStyle == "long" ? "T" : "t", targetCulture);
        }

        var format = (normalizedDateStyle, normalizedTimeStyle) switch
        {
            (null, null) => "G",
            ("short", null) => "d",
            ("long", null) => "D",
            (null, "short") => "t",
            (null, "long") => "T",
            ("short", "short") => "g",
            ("short", "long") => "G",
            ("long", "short") => "f",
            ("long", "long") => "F",
            _ => throw new InvalidOperationException("DATETIME could not select a date or time format.")
        };

        return _value.ToString(format, targetCulture);
    }

    private static string? NormalizeStyle(string? style, string optionName)
    {
        if (style is null)
        {
            return null;
        }

        var normalized = style.ToLowerInvariant();

        return normalized switch
        {
            "short" or "long" => normalized,
            _ => throw new InvalidOperationException(
                $"DATETIME option '{optionName}' must be short or long.")
        };
    }
}
