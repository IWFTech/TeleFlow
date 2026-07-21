namespace TeleFlow.Telegram.Internal.Options;

internal static class TelegramBotOptionsValidator
{
    public static void Validate(TelegramBotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.RoleFilter is null)
        {
            throw new InvalidOperationException("Telegram role filter options must be configured.");
        }

        if (!Enum.IsDefined(options.Environment))
        {
            throw new InvalidOperationException(
                $"Telegram Bot API environment '{options.Environment}' is not supported.");
        }

        if (options.RoleFilter.CacheTtl <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Telegram role filter cache TTL must be greater than zero.");
        }
    }
}
