using System.Text.Json;

namespace TeleFlow.Telegram.Internal;

internal interface ITelegramExecutableRequest<out TResponse> : ITelegramRequest<TResponse>
    where TResponse : ITelegramResponse
{
    string MethodName { get; }

    object Payload { get; }

    TResponse DeserializeResponse(JsonSerializerOptions serializerOptions, string resultJson);
}
