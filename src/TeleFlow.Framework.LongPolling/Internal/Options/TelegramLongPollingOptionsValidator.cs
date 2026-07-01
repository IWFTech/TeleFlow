namespace TeleFlow.Telegram.Internal.Options;

internal static class TelegramLongPollingOptionsValidator
{
    public static void Validate(TelegramLongPollingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Long polling timeout must be greater than zero.");
        }

        if (options.Limit is < 1 or > 100)
        {
            throw new InvalidOperationException("Long polling limit must be between 1 and 100.");
        }

        if (options.AllowedUpdates is null)
        {
            throw new InvalidOperationException("Long polling allowed updates options must be configured.");
        }

        ValidateBackoff(options.Backoff);
    }

    private static void ValidateBackoff(TelegramBackoffOptions? options)
    {
        if (options is null)
        {
            throw new InvalidOperationException("Long polling backoff options must be configured.");
        }

        if (options.MinDelay < TimeSpan.Zero)
        {
            throw new InvalidOperationException("Long polling backoff minimum delay must not be negative.");
        }

        if (options.MaxDelay < TimeSpan.Zero)
        {
            throw new InvalidOperationException("Long polling backoff maximum delay must not be negative.");
        }

        if (options.MaxDelay < options.MinDelay)
        {
            throw new InvalidOperationException("Long polling backoff maximum delay must be greater than or equal to minimum delay.");
        }

        if (options.Factor < 1)
        {
            throw new InvalidOperationException("Long polling backoff factor must be greater than or equal to one.");
        }

        if (options.Jitter is < 0 or > 1)
        {
            throw new InvalidOperationException("Long polling backoff jitter must be between zero and one.");
        }
    }
}
