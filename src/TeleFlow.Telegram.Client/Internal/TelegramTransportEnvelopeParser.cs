using System.Text.Json;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Parses raw Telegram Bot API response bytes into an owned envelope used by the request executor.
/// It keeps the parsed JSON document alive so successful result payloads can be deserialized without rematerializing JSON text.
/// </summary>
internal sealed class TelegramTransportEnvelopeParser
{
    private readonly JsonSerializerOptions _serializerOptions;

    public TelegramTransportEnvelopeParser(JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(serializerOptions);
        _serializerOptions = serializerOptions;
    }

    public bool TryParse(
        ReadOnlyMemory<byte> responseBody,
        out TelegramTransportEnvelope envelope,
        out JsonException? exception)
    {
        JsonDocument? document = null;

        try
        {
            document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            var ok = root.TryGetProperty("ok", out var okElement) && okElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? okElement.GetBoolean()
                : throw new JsonException("Telegram response does not contain a valid 'ok' property.");

            var hasResult = root.TryGetProperty("result", out var resultElement);

            string? description = null;
            if (root.TryGetProperty("description", out var descriptionElement) &&
                descriptionElement.ValueKind == JsonValueKind.String)
            {
                description = descriptionElement.GetString();
            }

            int? errorCode = null;
            if (root.TryGetProperty("error_code", out var errorCodeElement) &&
                errorCodeElement.ValueKind == JsonValueKind.Number &&
                errorCodeElement.TryGetInt32(out var parsedErrorCode))
            {
                errorCode = parsedErrorCode;
            }

            ResponseParameters? responseParameters = null;
            if (root.TryGetProperty("response_parameters", out var responseParametersElement))
            {
                responseParameters = JsonSerializer.Deserialize<ResponseParameters>(
                    responseParametersElement,
                    _serializerOptions);
            }

            envelope = new TelegramTransportEnvelope(
                document,
                ok,
                hasResult,
                resultElement,
                description,
                errorCode,
                responseParameters);
            document = null;
            exception = null;
            return true;
        }
        catch (JsonException jsonException)
        {
            envelope = null!;
            exception = jsonException;
            return false;
        }
        finally
        {
            document?.Dispose();
        }
    }
}
