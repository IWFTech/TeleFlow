using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure parameter binding kind emitted by TeleFlow source generators.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum TelegramGeneratedHandlerParameterKind
{
    Context,
    CallbackPayload,
    RouteValue,
    Service,
    CancellationToken
}
