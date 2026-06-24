using TeleFlow.Telegram.Internal;

namespace TeleFlow.Telegram;

public sealed class TelegramDeepLinks
{
    private readonly string? _botUsername;
    private readonly IDeepLinkPayloadSerializer _payloadSerializer;

    public TelegramDeepLinks(
        string? botUsername,
        IDeepLinkPayloadSerializer payloadSerializer)
    {
        ArgumentNullException.ThrowIfNull(payloadSerializer);

        if (!TelegramBotUsernameNormalizer.TryNormalize(botUsername, out var normalizedUsername, out var error))
        {
            throw new ArgumentException(error, nameof(botUsername));
        }

        _botUsername = normalizedUsername;
        _payloadSerializer = payloadSerializer;
    }

    public TelegramDeepLinks(
        TelegramClientOptions options,
        IDeepLinkPayloadSerializer payloadSerializer)
        : this(
            (options ?? throw new ArgumentNullException(nameof(options))).BotUsername,
            payloadSerializer)
    {
    }

    public Uri Start(string payload)
    {
        return Build("start", payload);
    }

    public Uri Start<TPayload>(TPayload payload)
    {
        return Start(Serialize(payload));
    }

    public Uri StartGroup(string payload)
    {
        return Build("startgroup", payload);
    }

    public Uri StartGroup<TPayload>(TPayload payload)
    {
        return StartGroup(Serialize(payload));
    }

    public string Serialize<TPayload>(TPayload payload)
    {
        var serialized = _payloadSerializer.Serialize(payload);
        TelegramDeepLinkPayloadValidator.Validate(serialized);
        return serialized;
    }

    public TPayload Deserialize<TPayload>(string payload)
    {
        TelegramDeepLinkPayloadValidator.Validate(payload);
        return _payloadSerializer.Deserialize<TPayload>(payload);
    }

    private Uri Build(string parameterName, string payload)
    {
        var username = GetConfiguredBotUsername();
        return TelegramDeepLinkUriBuilder.Build(username, parameterName, payload);
    }

    private string GetConfiguredBotUsername()
    {
        if (_botUsername is null)
        {
            throw new InvalidOperationException(
                "Telegram bot username must be configured before building deep links. Set TelegramClientOptions.BotUsername or TelegramBotOptions.BotUsername.");
        }

        return _botUsername;
    }
}
