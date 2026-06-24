namespace TeleFlow.Telegram.Webhooks.Internal;

internal static class TelegramRawWebhookOptionsValidator
{
    public static void Validate(TelegramRawWebhookOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.SecretToken is not null)
        {
            if (string.IsNullOrWhiteSpace(options.SecretToken))
            {
                throw new InvalidOperationException("Telegram webhook secret token must not be empty.");
            }

            options.SecretToken = options.SecretToken.Trim();
        }

        ValidateStatusCode(
            options.InvalidPayloadStatusCode,
            nameof(TelegramRawWebhookOptions.InvalidPayloadStatusCode));
        ValidateStatusCode(
            options.SecretTokenFailureStatusCode,
            nameof(TelegramRawWebhookOptions.SecretTokenFailureStatusCode));
    }

    private static void ValidateStatusCode(int statusCode, string optionName)
    {
        if (statusCode is < 100 or > 599)
        {
            throw new InvalidOperationException(
                $"{optionName} must be a valid HTTP status code between 100 and 599.");
        }
    }
}
