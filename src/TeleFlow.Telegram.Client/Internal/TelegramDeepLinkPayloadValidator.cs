using System.Text;

namespace TeleFlow.Telegram.Internal;

internal static class TelegramDeepLinkPayloadValidator
{
    public const int PayloadByteLimit = 64;

    public static void Validate(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        var byteCount = Encoding.UTF8.GetByteCount(payload);
        if (byteCount > PayloadByteLimit)
        {
            throw new ArgumentException(
                $"Telegram deep-link payload must be at most {PayloadByteLimit} UTF-8 bytes.",
                nameof(payload));
        }

        foreach (var character in payload)
        {
            if (!IsDeepLinkPayloadCharacter(character))
            {
                throw new ArgumentException(
                    "Telegram deep-link payload can contain only ASCII letters, digits, '-' and '_'.",
                    nameof(payload));
            }
        }
    }

    private static bool IsDeepLinkPayloadCharacter(char character)
    {
        return character is >= 'A' and <= 'Z' ||
               character is >= 'a' and <= 'z' ||
               character is >= '0' and <= '9' ||
               character is '-' or '_';
    }
}
