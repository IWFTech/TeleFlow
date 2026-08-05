using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Carries either a decoded Telegram update or the evidence required to apply an explicit poison-update policy.
/// </summary>
internal readonly record struct TelegramUpdateDecodeResult(
    long UpdateId,
    Update? Update,
    string? RawPayloadJson,
    string? PayloadSha256,
    TelegramUpdateDecodeException? Exception)
{
    public bool IsSuccess => Update is not null;

    public static TelegramUpdateDecodeResult Success(Update update)
    {
        ArgumentNullException.ThrowIfNull(update);
        return new TelegramUpdateDecodeResult(update.UpdateId, update, null, null, null);
    }

    public static TelegramUpdateDecodeResult Failure(
        long updateId,
        string rawPayloadJson,
        string payloadSha256,
        TelegramUpdateDecodeException exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPayloadJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadSha256);
        ArgumentNullException.ThrowIfNull(exception);
        return new TelegramUpdateDecodeResult(updateId, null, rawPayloadJson, payloadSha256, exception);
    }
}
