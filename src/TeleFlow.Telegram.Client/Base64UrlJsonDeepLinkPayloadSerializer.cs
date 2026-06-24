using System.Text;
using System.Text.Json;

namespace TeleFlow.Telegram;

public sealed class Base64UrlJsonDeepLinkPayloadSerializer : IDeepLinkPayloadSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Serialize<TPayload>(TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public TPayload Deserialize<TPayload>(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        try
        {
            var base64 = payload
                .Replace('-', '+')
                .Replace('_', '/');
            var padding = base64.Length % 4;
            if (padding != 0)
            {
                base64 = base64.PadRight(base64.Length + 4 - padding, '=');
            }

            var bytes = Convert.FromBase64String(base64);
            return JsonSerializer.Deserialize<TPayload>(bytes, JsonOptions)
                ?? throw new ArgumentException("Deep-link payload JSON deserialized to null.", nameof(payload));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new ArgumentException(
                "Deep-link payload is not a valid Base64Url JSON payload.",
                nameof(payload),
                exception);
        }
    }
}
