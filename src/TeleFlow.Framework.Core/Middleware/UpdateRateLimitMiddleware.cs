using TeleFlow.Framework.RateLimiting;
using TeleFlow.Framework.Updates;

namespace TeleFlow.Framework.Middleware;

public sealed class UpdateRateLimitMiddleware : IUpdateMiddleware
{
    private readonly IReadOnlyList<IUpdateRateLimiter> _rateLimiters;

    public UpdateRateLimitMiddleware(IEnumerable<IUpdateRateLimiter> rateLimiters)
    {
        ArgumentNullException.ThrowIfNull(rateLimiters);
        _rateLimiters = rateLimiters.ToArray();
    }

    public async Task InvokeAsync(UpdateContext context, UpdateDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        foreach (var rateLimiter in _rateLimiters)
        {
            await rateLimiter.WaitAsync(context, context.CancellationToken).ConfigureAwait(false);
        }

        await next(context).ConfigureAwait(false);
    }
}
