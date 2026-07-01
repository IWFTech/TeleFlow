using Microsoft.Extensions.Logging;
using TeleFlow.Framework.RateLimiting;
using TeleFlow.Framework.Updates;

namespace TeleFlow.Framework.Middleware;

/// <summary>
/// Runs registered update-level rate limiters before the dispatcher receives the update.
/// </summary>
public sealed partial class UpdateRateLimitMiddleware : IUpdateMiddleware
{
    private const int UpdateRejectedEventId = 1;

    private readonly IReadOnlyList<IUpdateRateLimiter> _rateLimiters;
    private readonly ILogger<UpdateRateLimitMiddleware>? _logger;

    public UpdateRateLimitMiddleware(IEnumerable<IUpdateRateLimiter> rateLimiters)
    {
        ArgumentNullException.ThrowIfNull(rateLimiters);

        _rateLimiters = rateLimiters.ToArray();
    }

    public UpdateRateLimitMiddleware(
        IEnumerable<IUpdateRateLimiter> rateLimiters,
        ILogger<UpdateRateLimitMiddleware> logger)
        : this(rateLimiters)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public async Task InvokeAsync(UpdateContext context, UpdateDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        foreach (var rateLimiter in _rateLimiters)
        {
            var decision = await rateLimiter
                .CheckAsync(context, context.CancellationToken)
                .ConfigureAwait(false);

            if (!decision.IsAccepted)
            {
                if (_logger is not null)
                {
                    LogUpdateRejected(
                        _logger,
                        context.Payload.GetType().Name,
                        rateLimiter.GetType().FullName ?? rateLimiter.GetType().Name,
                        decision.RetryAfter,
                        decision.PolicyName ?? string.Empty);
                }

                return;
            }
        }

        await next(context).ConfigureAwait(false);
    }

    [LoggerMessage(
        EventId = UpdateRejectedEventId,
        Level = LogLevel.Warning,
        Message = "Update rejected by rate limiter. payload_type={PayloadType}, limiter={Limiter}, retry_after={RetryAfter}, policy={PolicyName}.")]
    private static partial void LogUpdateRejected(
        ILogger logger,
        string payloadType,
        string limiter,
        TimeSpan? retryAfter,
        string policyName);
}
