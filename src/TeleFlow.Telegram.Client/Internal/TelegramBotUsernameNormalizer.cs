namespace TeleFlow.Telegram.Internal;

internal static class TelegramBotUsernameNormalizer
{
    private const int MinLength = 5;
    private const int MaxLength = 32;

    public static bool TryNormalize(
        string? botUsername,
        out string? normalizedUsername,
        out string? error)
    {
        if (botUsername is null)
        {
            normalizedUsername = null;
            error = null;
            return true;
        }

        var trimmed = botUsername.Trim();
        if (trimmed.StartsWith('@'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length is < MinLength or > MaxLength)
        {
            normalizedUsername = null;
            error = $"Telegram bot username must be between {MinLength} and {MaxLength} characters.";
            return false;
        }

        foreach (var character in trimmed)
        {
            if (!IsTelegramUsernameCharacter(character))
            {
                normalizedUsername = null;
                error = "Telegram bot username can contain only ASCII letters, digits, and underscores.";
                return false;
            }
        }

        normalizedUsername = trimmed;
        error = null;
        return true;
    }

    private static bool IsTelegramUsernameCharacter(char character)
    {
        return character is >= 'A' and <= 'Z' ||
               character is >= 'a' and <= 'z' ||
               character is >= '0' and <= '9' ||
               character == '_';
    }
}
