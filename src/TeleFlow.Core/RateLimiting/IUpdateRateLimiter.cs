using TeleFlow.Core.Updates;

namespace TeleFlow.Core.RateLimiting;

public interface IUpdateRateLimiter
{
    ValueTask WaitAsync(UpdateContext context, CancellationToken cancellationToken = default);
}
