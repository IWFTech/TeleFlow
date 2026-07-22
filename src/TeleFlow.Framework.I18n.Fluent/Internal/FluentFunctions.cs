using System.Globalization;
using Linguini.Bundle.Types;
using Linguini.Shared.Types.Bundle;

namespace TeleFlow.Telegram.I18n.Fluent.Internal;

/// <summary>
/// Implements TeleFlow's documented NUMBER and DATETIME Fluent functions with locale-aware, mode-safe output.
/// </summary>
internal static class FluentFunctions
{
    public static ExternalFunction CreateNumber(CultureInfo culture, FluentRenderingMode mode)
    {
        return (arguments, namedArguments) =>
        {
            if (arguments.Count != 1 || arguments[0] is not FluentNumber number)
            {
                throw new InvalidOperationException("NUMBER requires exactly one numeric positional argument.");
            }

            var minimumFractionDigits = GetIntegerOption(namedArguments, "minimumFractionDigits", 0, 100) ?? 0;
            var maximumFractionDigits = GetIntegerOption(namedArguments, "maximumFractionDigits", 0, 100) ??
                                        Math.Max(minimumFractionDigits, 3);

            if (maximumFractionDigits < minimumFractionDigits)
            {
                throw new InvalidOperationException(
                    "NUMBER maximumFractionDigits must be greater than or equal to minimumFractionDigits.");
            }

            var useGrouping = GetBooleanOption(namedArguments, "useGrouping") ?? true;
            var formatted = FormatNumber(
                number.Value,
                culture,
                useGrouping,
                minimumFractionDigits,
                maximumFractionDigits);

            return (FluentString)FluentValueConverter.Escape(formatted, mode);
        };
    }

    public static ExternalFunction CreateDateTime(CultureInfo culture, FluentRenderingMode mode)
    {
        return (arguments, namedArguments) =>
        {
            if (arguments.Count != 1 || arguments[0] is not FluentDateTimeValue dateTime)
            {
                throw new InvalidOperationException(
                    "DATETIME requires exactly one DateTime, DateTimeOffset, DateOnly, or TimeOnly argument.");
            }

            var dateStyle = GetStringOption(namedArguments, "dateStyle");
            var timeStyle = GetStringOption(namedArguments, "timeStyle");
            var formatted = dateTime.Format(culture, dateStyle, timeStyle);
            return (FluentString)FluentValueConverter.Escape(formatted, mode);
        };
    }

    private static string FormatNumber(
        double value,
        CultureInfo culture,
        bool useGrouping,
        int minimumFractionDigits,
        int maximumFractionDigits)
    {
        var format = (useGrouping ? "N" : "F") + maximumFractionDigits.ToString(CultureInfo.InvariantCulture);
        var formatted = value.ToString(format, culture);

        if (maximumFractionDigits == minimumFractionDigits)
        {
            return formatted;
        }

        var separator = culture.NumberFormat.NumberDecimalSeparator;
        var separatorIndex = formatted.LastIndexOf(separator, StringComparison.Ordinal);

        if (separatorIndex < 0)
        {
            return formatted;
        }

        var trimEnd = formatted.Length;
        var minimumEnd = separatorIndex + separator.Length + minimumFractionDigits;

        while (trimEnd > minimumEnd && formatted[trimEnd - 1] == '0')
        {
            trimEnd--;
        }

        if (trimEnd == separatorIndex + separator.Length)
        {
            trimEnd = separatorIndex;
        }

        return formatted[..trimEnd];
    }

    private static int? GetIntegerOption(
        IDictionary<string, IFluentType> arguments,
        string name,
        int minimum,
        int maximum)
    {
        if (!arguments.TryGetValue(name, out var value))
        {
            return null;
        }

        if (value is not FluentNumber number ||
            number.Value % 1 != 0 ||
            number.Value < minimum ||
            number.Value > maximum)
        {
            throw new InvalidOperationException(
                $"Fluent option '{name}' must be an integer from {minimum} through {maximum}.");
        }

        return (int)number.Value;
    }

    private static bool? GetBooleanOption(IDictionary<string, IFluentType> arguments, string name)
    {
        var value = GetStringOption(arguments, name);

        if (value is null)
        {
            return null;
        }

        return value.ToUpperInvariant() switch
        {
            "TRUE" or "1" => true,
            "FALSE" or "0" => false,
            _ => throw new InvalidOperationException(
                $"Fluent option '{name}' must be true, false, 1, or 0.")
        };
    }

    private static string? GetStringOption(IDictionary<string, IFluentType> arguments, string name)
    {
        return arguments.TryGetValue(name, out var value)
            ? value.AsString()
            : null;
    }
}
