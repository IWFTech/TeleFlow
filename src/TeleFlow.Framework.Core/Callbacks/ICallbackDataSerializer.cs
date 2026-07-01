namespace TeleFlow.Framework.Callbacks;

public interface ICallbackDataSerializer
{
    string Serialize<TPayload>(TPayload payload);

    /// <summary>
    /// Deserializes Telegram callback data. Throw <see cref="System.Text.Json.JsonException"/>,
    /// <see cref="FormatException"/>, or <see cref="OverflowException"/> when the payload is invalid
    /// and the typed callback route should not match; other exceptions are treated as serializer failures.
    /// </summary>
    TPayload Deserialize<TPayload>(string serializedPayload);
}
