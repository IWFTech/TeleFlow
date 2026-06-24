using System.Text.Json;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

internal sealed class TelegramTransportEnvelopeParser
{
    private readonly JsonSerializerOptions _serializerOptions;

    public TelegramTransportEnvelopeParser(JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(serializerOptions);
        _serializerOptions = serializerOptions;
    }

    public bool TryParse(
        string responseText,
        out TelegramTransportEnvelope envelope,
        out JsonException? exception)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;

            var ok = root.TryGetProperty("ok", out var okElement) && okElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? okElement.GetBoolean()
                : throw new JsonException("Telegram response does not contain a valid 'ok' property.");

            string? resultJson = null;
            if (root.TryGetProperty("result", out var resultElement))
            {
                resultJson = resultElement.GetRawText();
            }

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
                    responseParametersElement.GetRawText(),
                    _serializerOptions);
            }

            envelope = new TelegramTransportEnvelope
            {
                Ok = ok,
                ResultJson = resultJson,
                Description = description,
                ErrorCode = errorCode,
                ResponseParameters = responseParameters
            };
            exception = null;
            return true;
        }
        catch (JsonException jsonException)
        {
            envelope = null!;
            exception = jsonException;
            return false;
        }
    }
}
