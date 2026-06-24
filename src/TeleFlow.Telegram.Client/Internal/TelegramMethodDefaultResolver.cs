namespace TeleFlow.Telegram.Internal;

internal static class TelegramMethodDefaultResolver
{
    public static string? ResolveParseMode(
        ITelegramClient bot,
        TelegramParseMode? parseMode,
        object? entities = null)
    {
        ArgumentNullException.ThrowIfNull(bot);

        if (parseMode is { } explicitParseMode)
        {
            return explicitParseMode.Value;
        }

        if (entities is not null)
        {
            return null;
        }

        return bot.Defaults.ParseMode?.Value;
    }
}
