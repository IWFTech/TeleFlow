using System.Text.Json;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Internal executable form of a Telegram API request used by the request executor after public client calls are normalized.
/// It deserializes the already-parsed Telegram result element while the transport envelope is still alive.
/// </summary>
internal interface ITelegramExecutableRequest<out TResponse> : ITelegramRequest<TResponse>
    where TResponse : ITelegramResponse
{
    string MethodName { get; }

    object Payload { get; }

    TResponse DeserializeResponse(JsonSerializerOptions serializerOptions, JsonElement result);
}
