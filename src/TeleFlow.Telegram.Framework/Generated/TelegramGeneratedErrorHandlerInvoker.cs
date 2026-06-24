using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure invoker emitted by TeleFlow source generators.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask<TelegramErrorHandlingResult> TelegramGeneratedErrorHandlerInvoker(
    IServiceProvider services,
    object?[] arguments,
    CancellationToken cancellationToken);
