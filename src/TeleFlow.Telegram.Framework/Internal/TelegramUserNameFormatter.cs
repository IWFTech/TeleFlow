namespace TeleFlow.Telegram.Internal;

internal static class TelegramUserNameFormatter
{
    public static string GetFullName(string? firstName, string? lastName)
    {
        var trimmedFirstName = firstName?.Trim() ?? string.Empty;
        var trimmedLastName = lastName?.Trim() ?? string.Empty;

        return (trimmedFirstName, trimmedLastName) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{trimmedFirstName} {trimmedLastName}",
            ({ Length: > 0 }, _) => trimmedFirstName,
            (_, { Length: > 0 }) => trimmedLastName,
            _ => string.Empty
        };
    }
}
