using TeleFlow.Framework.Updates;

namespace TeleFlow.Framework.RateLimiting;

public interface IUpdateRateLimiter
{
    ValueTask WaitAsync(UpdateContext context, CancellationToken cancellationToken = default);
}
