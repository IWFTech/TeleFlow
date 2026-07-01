using System.Text.Json;
using TeleFlow.Framework.Callbacks;
using TeleFlow.Telegram.Internal;

namespace TeleFlow.Telegram;

/// <summary>
/// Serializes callback payloads for typed callback handlers, using compact
/// <see cref="TeleFlow.Annotations.CallbackDataAttribute"/> metadata when available and JSON otherwise.
/// </summary>
public sealed class JsonCallbackDataSerializer : ICallbackDataRouteDeserializer
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public JsonCallbackDataSerializer(TelegramJsonOptions jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(jsonOptions);
        _jsonSerializerOptions = jsonOptions.SerializerOptions;
    }

    public string Serialize<TPayload>(TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var serializedPayload = CallbackDataMetadata.TryCreate(typeof(TPayload), out var metadata)
            ? CallbackDataCodec.Pack(payload!, metadata)
            : JsonSerializer.Serialize(payload, _jsonSerializerOptions);

        CallbackDataCodec.ValidateCallbackData(serializedPayload, "Serialized Telegram callback data");

        return serializedPayload;
    }

    public TPayload Deserialize<TPayload>(string serializedPayload)
    {
        ArgumentNullException.ThrowIfNull(serializedPayload);

        if (CallbackDataMetadata.TryCreate(typeof(TPayload), out var metadata))
        {
            return (TPayload)CallbackDataCodec.Unpack(serializedPayload, metadata);
        }

        return JsonSerializer.Deserialize<TPayload>(serializedPayload, _jsonSerializerOptions)
            ?? throw new JsonException("Telegram callback data deserialized to null.");
    }

    bool ICallbackDataRouteDeserializer.TryDeserializeForRoute(
        Type payloadType,
        string serializedPayload,
        out object? payload)
    {
        ArgumentNullException.ThrowIfNull(payloadType);
        ArgumentNullException.ThrowIfNull(serializedPayload);

        if (CallbackDataMetadata.TryCreate(payloadType, out var metadata))
        {
            if (!metadata.MatchesSerializedPayload(serializedPayload))
            {
                payload = null;
                return false;
            }

            payload = CallbackDataCodec.Unpack(serializedPayload, metadata);
            return true;
        }

        payload = DeserializeJson(payloadType, serializedPayload);
        return true;
    }

    private object DeserializeJson(Type payloadType, string serializedPayload)
    {
        return JsonSerializer.Deserialize(serializedPayload, payloadType, _jsonSerializerOptions)
            ?? throw new JsonException("Telegram callback data deserialized to null.");
    }
}
