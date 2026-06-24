namespace TeleFlow.Telegram.Internal;

internal static class TelegramRawLongPollingOptionsValidator
{
    public static void Validate(TelegramRawLongPollingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Raw long polling timeout must be greater than zero.");
        }

        if (options.Limit is < 1 or > 100)
        {
            throw new InvalidOperationException("Raw long polling limit must be between 1 and 100.");
        }

        ValidateAllowedUpdates(options.AllowedUpdates);
        TelegramRawLongPollingBackoff.ValidateOptions(options.Backoff);
    }

    private static void ValidateAllowedUpdates(IReadOnlyList<string>? allowedUpdates)
    {
        if (allowedUpdates is null)
        {
            return;
        }

        foreach (var updateType in allowedUpdates)
        {
            if (string.IsNullOrWhiteSpace(updateType))
            {
                throw new InvalidOperationException("Raw long polling allowed update values must not be empty.");
            }
        }

        var duplicate = allowedUpdates
            .GroupBy(static updateType => updateType, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate raw long polling allowed update value '{duplicate.Key}' was specified.");
        }
    }
}
