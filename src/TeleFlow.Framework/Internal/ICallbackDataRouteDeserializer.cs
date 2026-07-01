using TeleFlow.Framework.Callbacks;

namespace TeleFlow.Telegram.Internal;

internal interface ICallbackDataRouteDeserializer : ICallbackDataSerializer
{
    bool TryDeserializeForRoute(Type payloadType, string serializedPayload, out object? payload);
}
