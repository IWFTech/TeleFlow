using TeleFlow.Framework.Updates;

namespace TeleFlow.Framework.RateLimiting;

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
