namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Returns the ordered per-update outcomes produced by the specialized getUpdates decoder.
/// </summary>
internal sealed record TelegramUpdateBatchResponse(
    IReadOnlyList<TelegramUpdateDecodeResult> Items) : ITelegramResponse;
