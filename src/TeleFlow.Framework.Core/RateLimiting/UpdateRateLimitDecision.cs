namespace TeleFlow.Framework.RateLimiting;

/// <summary>
/// Describes whether an incoming update can continue through the framework pipeline after
/// update-level rate limiting has been evaluated.
/// </summary>
public readonly record struct UpdateRateLimitDecision
{
    private const byte AcceptedKind = 0;
    private const byte RejectedKind = 1;

    private readonly byte _kind;

    private UpdateRateLimitDecision(
        byte kind,
        TimeSpan? retryAfter,
        string? policyName)
    {
        if (retryAfter < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryAfter),
                retryAfter,
                "Rate-limit retry delay must not be negative.");
        }

        _kind = kind;
        RetryAfter = retryAfter;
        PolicyName = string.IsNullOrWhiteSpace(policyName) ? null : policyName;
    }

    /// <summary>
    /// Gets a decision that allows the update to continue through the framework pipeline.
    /// </summary>
    public static UpdateRateLimitDecision Accepted => default;

    /// <summary>
    /// Gets whether the update is allowed to continue through the framework pipeline.
    /// </summary>
    public bool IsAccepted => _kind == AcceptedKind;

    /// <summary>
    /// Gets whether the update was rejected by a rate limiter.
    /// </summary>
    public bool IsRejected => _kind == RejectedKind;

    /// <summary>
    /// Gets the optional delay after which the update sender may retry, when the limiter can provide it.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Gets the optional developer-controlled policy name that rejected the update.
    /// </summary>
    public string? PolicyName { get; }

    /// <summary>
    /// Creates a decision that rejects the update and stops the framework pipeline.
    /// </summary>
    public static UpdateRateLimitDecision Rejected(
        TimeSpan? retryAfter = null,
        string? policyName = null)
    {
        return new UpdateRateLimitDecision(RejectedKind, retryAfter, policyName);
    }
}
