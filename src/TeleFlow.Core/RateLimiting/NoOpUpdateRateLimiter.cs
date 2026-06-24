using TeleFlow.Core.Updates;

namespace TeleFlow.Core.RateLimiting;

public sealed class NoOpUpdateRateLimiter : IUpdateRateLimiter
{
    public ValueTask WaitAsync(
        UpdateContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.CompletedTask;
    }
}
