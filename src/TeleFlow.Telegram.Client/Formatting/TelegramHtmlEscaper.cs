using System.Text;

namespace TeleFlow.Telegram.Formatting;

/// <summary>
/// Encodes dynamic values for Telegram HTML text and interpolation boundaries in one pass.
/// Text rendering preserves quotes, while general interpolation also prevents values from leaving quoted attributes.
/// </summary>
internal static class TelegramHtmlEscaper
{
    public static string EscapeText(string value)
    {
        return Escape(value, escapeQuotes: false);
    }

    public static string EscapeInterpolation(string value)
    {
        return Escape(value, escapeQuotes: true);
    }

    private static string Escape(string value, bool escapeQuotes)
    {
        var firstSpecialCharacter = FindFirstSpecialCharacter(value, escapeQuotes);

        if (firstSpecialCharacter < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 16);
        builder.Append(value.AsSpan(0, firstSpecialCharacter));

        for (var index = firstSpecialCharacter; index < value.Length; index++)
        {
            var replacement = value[index] switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' when escapeQuotes => "&quot;",
                '\'' when escapeQuotes => "&#39;",
                _ => null
            };

            if (replacement is null)
            {
                builder.Append(value[index]);
            }
            else
            {
                builder.Append(replacement);
            }
        }

        return builder.ToString();
    }

    private static int FindFirstSpecialCharacter(string value, bool escapeQuotes)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] is '&' or '<' or '>' ||
                escapeQuotes && (value[index] is '"' or '\''))
            {
                return index;
            }
        }

        return -1;
    }
}
