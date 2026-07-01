using System.Text;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Represents a typed callback route payload that matched the expected callback data
/// shape but could not be decoded before handler selection.
/// </summary>
internal sealed class CallbackDataRouteDeserializationException : Exception
{
    public CallbackDataRouteDeserializationException(
        Type payloadType,
        string serializedPayload,
        Exception innerException)
        : base(
            CreateMessage(payloadType),
            innerException ?? throw new ArgumentNullException(nameof(innerException)))
    {
        ArgumentNullException.ThrowIfNull(payloadType);
        ArgumentNullException.ThrowIfNull(serializedPayload);

        PayloadType = payloadType;
        PayloadByteCount = Encoding.UTF8.GetByteCount(serializedPayload);
    }

    public Type PayloadType { get; }

    public int PayloadByteCount { get; }

    private static string CreateMessage(Type payloadType)
    {
        ArgumentNullException.ThrowIfNull(payloadType);

        return $"Telegram callback data matched payload type '{payloadType.FullName}' but could not be deserialized.";
    }
}
