using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure delegate emitted by TeleFlow source generators for direct handler invocation.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask TelegramGeneratedHandlerInvoker(
    IServiceProvider services,
    object?[] arguments,
    CancellationToken cancellationToken);
