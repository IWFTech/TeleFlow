namespace TeleFlow.Telegram.Internal.Options;

internal static class TelegramClientOptionsValidator
{
    public static void Validate(TelegramClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Token))
        {
            throw new InvalidOperationException("Telegram bot token must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException("Telegram base URL must be configured.");
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Telegram base URL must be an absolute URI.");
        }

        if (!TelegramBotUsernameNormalizer.TryNormalize(options.BotUsername, out _, out var botUsernameError))
        {
            throw new InvalidOperationException(botUsernameError);
        }

        if (options.Defaults is null)
        {
            throw new InvalidOperationException("Telegram bot defaults must be configured.");
        }
    }
}
