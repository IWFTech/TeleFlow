using System.Text.Json;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Builds the transport-level request body for Telegram API method payloads.
/// It keeps the normal JSON path cheap and delegates multipart-specific work only when upload data is present.
/// This is reached by the Telegram request executor for every outgoing Bot API call, including ctx.Bot.*Async helpers.
/// </summary>
internal sealed class TelegramRequestContentBuilder
{
    private readonly JsonSerializerOptions _serializerOptions;

    public TelegramRequestContentBuilder(JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(serializerOptions);
        _serializerOptions = serializerOptions;
    }

    public TelegramTransportContent Build(object payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var payloadType = payload.GetType();

        // Telegram requests are JSON unless a concrete value contains an InputFile stream.
        // The type check is the cheap hot-path proof; the value scan only runs for upload-capable shapes.
        if (!TelegramRequestUploadDetector.MayContainInputFile(payloadType) ||
            !TelegramRequestUploadDetector.ContainsInputFile(payload))
        {
            var json = JsonSerializer.Serialize(payload, payloadType, _serializerOptions);
            return new TelegramJsonTransportContent(json);
        }

        var builder = new TelegramMultipartContentBuilder(_serializerOptions);
        return builder.Build(payload);
    }
}
