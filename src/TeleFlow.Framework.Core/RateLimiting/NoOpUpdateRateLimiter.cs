using TeleFlow.Framework.Updates;

namespace TeleFlow.Framework.RateLimiting;

/// <summary>
/// Allows every update through the update-level rate-limiting stage.
/// </summary>
public sealed class NoOpUpdateRateLimiter : IUpdateRateLimiter
{
    public ValueTask<UpdateRateLimitDecision> CheckAsync(
        UpdateContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(UpdateRateLimitDecision.Accepted);
    }
}
