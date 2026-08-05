using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Decodes one known Telegram update envelope and captures raw payload evidence only when schema mapping fails.
/// </summary>
internal static class TelegramUpdateDecoder
{
    public static TelegramUpdateDecodeResult Decode(
        JsonElement payload,
        long updateId,
        JsonSerializerOptions serializerOptions,
        string? methodName = null)
    {
        ArgumentNullException.ThrowIfNull(serializerOptions);

        try
        {
            var update = payload.Deserialize<Update>(serializerOptions)
                ?? throw new JsonException("Telegram update payload deserialized to null.");

            return TelegramUpdateDecodeResult.Success(update);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            var rawPayloadJson = payload.GetRawText();
            var payloadSha256 = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(rawPayloadJson)));
            var jsonPath = exception is JsonException jsonException ? jsonException.Path : null;
            var decodeException = new TelegramUpdateDecodeException(
                $"Failed to deserialize Telegram update '{updateId}'.",
                updateId,
                payloadSha256,
                jsonPath,
                exception,
                methodName);

            return TelegramUpdateDecodeResult.Failure(
                updateId,
                rawPayloadJson,
                payloadSha256,
                decodeException);
        }
    }
}
