using TeleFlow.Framework.Updates;

namespace TeleFlow.Framework.RateLimiting;

/// <summary>
/// Evaluates whether an incoming update may continue through the framework pipeline before dispatch.
/// </summary>
public interface IUpdateRateLimiter
{
    /// <summary>
    /// Checks the current update and returns an explicit decision instead of using exceptions for expected throttling.
    /// </summary>
    ValueTask<UpdateRateLimitDecision> CheckAsync(
        UpdateContext context,
        CancellationToken cancellationToken = default);
}
