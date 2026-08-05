namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Preserves Telegram updates by stopping transport progress when an application has not installed an explicit quarantine policy.
/// </summary>
internal sealed class StopTelegramUpdateDecodeFailurePolicy : ITelegramUpdateDecodeFailurePolicy
{
    public static StopTelegramUpdateDecodeFailurePolicy Instance { get; } = new();

    private StopTelegramUpdateDecodeFailurePolicy()
    {
    }

    public ValueTask<TelegramUpdateDecodeFailureDecision> DecideAsync(
        TelegramUpdateDecodeFailure failure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(failure);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(TelegramUpdateDecodeFailureDecision.Stop);
    }
}
