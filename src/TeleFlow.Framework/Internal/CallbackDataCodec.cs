using System.Text;
using System.Text.Json;
using TeleFlow.Annotations;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Packs and unpacks compact Telegram callback payloads used by typed callback handlers
/// and inline keyboard builders before data is sent to or received from Telegram.
/// </summary>
internal static class CallbackDataCodec
{
    public const int MaxTelegramCallbackDataBytes = 64;

    public static string PackRequired(object payload, Type payloadType)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(payloadType);

        if (GeneratedCallbackDataCodecRegistry.TryGet(payloadType, out var generatedCodec))
        {
            return generatedCodec.Pack(payload);
        }

        if (!CallbackDataMetadata.TryCreate(payloadType, out var metadata))
        {
            throw new InvalidOperationException(
                $"Inline keyboard typed callback payload '{payloadType.FullName}' must be marked with " +
                $"{nameof(CallbackDataAttribute)}. Add [CallbackData(\"prefix\")] to the payload type or pass raw string callback data.");
        }

        return Pack(payload, metadata);
    }

    public static bool TryGetGenerated(Type payloadType, out GeneratedCallbackDataCodec codec)
    {
        return GeneratedCallbackDataCodecRegistry.TryGet(payloadType, out codec);
    }

    public static string Pack(object payload, CallbackDataMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(metadata);

        var serializedPayload = metadata.Pack(payload);
        ValidateCallbackData(serializedPayload, "Serialized Telegram callback data");
        return serializedPayload;
    }

    public static object Unpack(string serializedPayload, CallbackDataMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(serializedPayload);
        ArgumentNullException.ThrowIfNull(metadata);

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

    public static void ValidateCallbackData(string callbackData, string subject)
    {
        ArgumentNullException.ThrowIfNull(callbackData);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        if (string.IsNullOrWhiteSpace(callbackData))
        {
            throw new InvalidOperationException($"{subject} must not be empty.");
        }

        if (Encoding.UTF8.GetByteCount(callbackData) > MaxTelegramCallbackDataBytes)
        {
            throw new InvalidOperationException(
                $"{subject} must be at most {MaxTelegramCallbackDataBytes} UTF-8 bytes.");
        }
    }
}
