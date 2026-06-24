using System.Text;
using System.Text.Json;
using TeleFlow.Core.Callbacks;
using TeleFlow.Telegram.Internal;

namespace TeleFlow.Telegram;

public sealed class JsonCallbackDataSerializer : ICallbackDataSerializer
{
    private const int MaxTelegramCallbackDataBytes = 64;

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
            ? SerializeCompact(payload, metadata)
            : JsonSerializer.Serialize(payload, _jsonSerializerOptions);

        var bytes = Encoding.UTF8.GetBytes(serializedPayload);

        if (bytes.Length > MaxTelegramCallbackDataBytes)
        {
            throw new InvalidOperationException(
                $"Serialized Telegram callback data must be at most {MaxTelegramCallbackDataBytes} UTF-8 bytes.");
        }

        return serializedPayload;
    }

    public TPayload Deserialize<TPayload>(string serializedPayload)
    {
        ArgumentNullException.ThrowIfNull(serializedPayload);

        if (CallbackDataMetadata.TryCreate(typeof(TPayload), out var metadata))
        {
            return (TPayload)DeserializeCompact(serializedPayload, metadata);
        }

        return JsonSerializer.Deserialize<TPayload>(serializedPayload, _jsonSerializerOptions)
            ?? throw new JsonException("Telegram callback data deserialized to null.");
    }

    private static string SerializeCompact<TPayload>(TPayload payload, CallbackDataMetadata metadata)
    {
        var values = metadata.Fields
            .Select(field => metadata.FormatField(field.Property.GetValue(payload), field.Property.PropertyType));

        return string.Join(':', values.Prepend(metadata.Prefix));
    }

    private static object DeserializeCompact(string serializedPayload, CallbackDataMetadata metadata)
    {
        var parts = serializedPayload.Split(':');

        if (parts.Length == 0 ||
            !string.Equals(parts[0], metadata.Prefix, StringComparison.Ordinal))
        {
            throw new JsonException(
                $"Telegram callback data prefix does not match payload type '{metadata.PayloadType.FullName}'.");
        }

        if (parts.Length - 1 != metadata.Fields.Count)
        {
            throw new JsonException(
                $"Telegram callback data field count does not match payload type '{metadata.PayloadType.FullName}'.");
        }

        var values = metadata.Fields
            .Select((field, index) => metadata.ParseField(parts[index + 1], field.Property.PropertyType))
            .ToArray();

        if (metadata.Constructor is not null)
        {
            return metadata.Constructor.Invoke(values);
        }

        var payload = Activator.CreateInstance(metadata.PayloadType)
            ?? throw new JsonException($"Unable to create callback data payload type '{metadata.PayloadType.FullName}'.");

        for (var index = 0; index < metadata.Fields.Count; index++)
        {
            metadata.Fields[index].Property.SetValue(payload, values[index]);
        }

        return payload;
    }
}
