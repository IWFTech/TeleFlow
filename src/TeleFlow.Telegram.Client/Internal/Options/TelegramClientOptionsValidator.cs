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

        if (!Enum.IsDefined(options.Environment))
        {
            throw new InvalidOperationException(
                $"Telegram Bot API environment '{options.Environment}' is not supported.");
        }

        if (!TelegramBotUsernameNormalizer.TryNormalize(options.BotUsername, out _, out var botUsernameError))
        {
            throw new InvalidOperationException(botUsernameError);
        }

        if (options.Defaults is null)
        {
            throw new InvalidOperationException("Telegram bot defaults must be configured.");
        }

        ValidateRetryAfter(options.RetryAfter);
    }

    private static void ValidateRetryAfter(TelegramRetryAfterPolicy? policy)
    {
        if (policy is null)
        {
            throw new InvalidOperationException("Telegram retry-after policy must be configured.");
        }

        if (policy.MaxRetries < 0)
        {
            throw new InvalidOperationException("Telegram retry-after maximum retry count must not be negative.");
        }

        if (policy.MaxDelay <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Telegram retry-after maximum retry delay must be greater than zero.");
        }
    }
}
