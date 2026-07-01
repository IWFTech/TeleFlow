namespace TeleFlow.Telegram;

/// <summary>
/// Controls how the Telegram client reacts to Bot API throttling responses that include retry timing metadata.
/// This policy is used by the request executor for outgoing Bot API calls such as generated ctx.Bot.*Async methods.
/// </summary>
public sealed record class TelegramRetryAfterPolicy
{
    /// <summary>
    /// Production-safe default that retries one short Telegram throttling response before surfacing the error.
    /// </summary>
    public static TelegramRetryAfterPolicy Default { get; } = new()
    {
        Enabled = true,
        MaxRetries = 1,
        MaxDelay = TimeSpan.FromSeconds(5)
    };

    /// <summary>
    /// Disables automatic retry-after waiting and surfaces Telegram throttling responses immediately.
    /// </summary>
    public static TelegramRetryAfterPolicy Disabled { get; } = new()
    {
        Enabled = false,
        MaxRetries = 0,
        MaxDelay = TimeSpan.FromSeconds(5)
    };

    /// <summary>
    /// Gets whether automatic retry-after waiting is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets the maximum number of automatic retry attempts after the first throttled response.
    /// </summary>
    public int MaxRetries { get; init; } = 1;

    /// <summary>
    /// Gets the maximum Telegram-requested delay that the executor may wait automatically.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(5);
}
