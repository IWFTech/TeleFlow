using TeleFlow.Telegram.Schema.Methods;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Receives one getUpdates result while preserving per-update decode failures instead of failing the complete batch.
/// </summary>
internal interface ITelegramUpdateBatchReceiver
{
    Task<IReadOnlyList<TelegramUpdateDecodeResult>> ReceiveAsync(
        GetUpdates request,
        CancellationToken cancellationToken);
}
