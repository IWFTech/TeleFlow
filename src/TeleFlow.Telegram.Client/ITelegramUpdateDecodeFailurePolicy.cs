namespace TeleFlow.Telegram;

/// <summary>
/// Decides how Telegram transports handle an individual update that cannot be decoded by the installed schema.
/// Returning <see cref="TelegramUpdateDecodeFailureDecision.Skip"/> acknowledges data loss and should happen only after durable quarantine succeeds.
/// </summary>
public interface ITelegramUpdateDecodeFailurePolicy
{
    /// <summary>
    /// Returns the acknowledgement decision after any application-owned quarantine work completes.
    /// </summary>
    ValueTask<TelegramUpdateDecodeFailureDecision> DecideAsync(
        TelegramUpdateDecodeFailure failure,
        CancellationToken cancellationToken = default);
}
