using System.Text.Json;
using TeleFlow.Telegram.Schema.Methods;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Adapts getUpdates to a response contract that isolates schema failures to individual update elements.
/// </summary>
internal sealed class TelegramUpdateBatchRequest(GetUpdates request) :
    ITelegramExecutableRequest<TelegramUpdateBatchResponse>
{
    private readonly GetUpdates _request = request ?? throw new ArgumentNullException(nameof(request));

    public string MethodName => GetUpdates.MethodName;

    public object Payload => _request;

    public TelegramUpdateBatchResponse DeserializeResponse(
        JsonSerializerOptions serializerOptions,
        JsonElement result)
    {
        ArgumentNullException.ThrowIfNull(serializerOptions);

        if (result.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Telegram getUpdates result must be an array.");
        }

        var items = new List<TelegramUpdateDecodeResult>(result.GetArrayLength());
        foreach (var payload in result.EnumerateArray())
        {
            if (payload.ValueKind != JsonValueKind.Object ||
                !payload.TryGetProperty("update_id", out var updateIdElement) ||
                updateIdElement.ValueKind != JsonValueKind.Number ||
                !updateIdElement.TryGetInt64(out var updateId))
            {
                throw new JsonException("Telegram update payload does not contain a valid 'update_id'.");
            }

            items.Add(TelegramUpdateDecoder.Decode(
                payload,
                updateId,
                serializerOptions,
                MethodName));
        }

        return new TelegramUpdateBatchResponse(items);
    }
}
