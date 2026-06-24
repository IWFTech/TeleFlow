namespace TeleFlow.Telegram.Internal;

internal static class TelegramDeepLinkUriBuilder
{
    public static Uri Build(string botUsername, string parameterName, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(botUsername);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);

        TelegramDeepLinkPayloadValidator.Validate(payload);
        return new Uri($"https://t.me/{botUsername}?{parameterName}={payload}", UriKind.Absolute);
    }
}
