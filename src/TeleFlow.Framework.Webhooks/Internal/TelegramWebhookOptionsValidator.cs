namespace TeleFlow.Telegram.Webhooks.Internal;

internal static class TelegramWebhookOptionsValidator
{
    public static void Validate(TelegramWebhookOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Path))
        {
            throw new InvalidOperationException("Webhook path must not be empty.");
        }

        options.Path = options.Path.Trim();

        if (!options.Path.StartsWith('/'))
        {
            throw new InvalidOperationException("Webhook path must start with '/'.");
        }

        if (options.SecretToken is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.SecretToken))
        {
            throw new InvalidOperationException("Webhook secret token must not be empty.");
        }

        options.SecretToken = options.SecretToken.Trim();
    }
}
