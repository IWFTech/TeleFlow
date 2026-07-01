using System.Globalization;

namespace TeleFlow.Telegram.Internal;

internal static class TelegramRetryAfterDelayResolver
{
    public static TelegramRetryAfterDelay? ResolveDelay(
        TelegramTransportResponse response,
        TelegramTransportEnvelope? envelope,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (envelope?.ResponseParameters?.RetryAfter is long retryAfterFromEnvelope)
        {
            return CreateDelay(retryAfterFromEnvelope);
        }

        return response.TryGetHeaderValues("Retry-After", out var values)
            ? ResolveHeaderDelay(values, timeProvider)
            : null;
    }

    public static Task DelayAsync(
        TelegramRetryAfterDelay delay,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        return Task.Delay(delay.Value, timeProvider, cancellationToken);
    }

    private static TelegramRetryAfterDelay? ResolveHeaderDelay(
        IReadOnlyList<string> retryAfterHeaderValues,
        TimeProvider timeProvider)
    {
        foreach (var value in retryAfterHeaderValues)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var deltaSeconds))
            {
                return CreateDelay(deltaSeconds);
            }

            if (DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var date))
            {
                var seconds = Math.Ceiling((date - timeProvider.GetUtcNow()).TotalSeconds);
                return CreateDelay((long)Math.Max(seconds, 0));
            }
        }

        return null;
    }

    private static TelegramRetryAfterDelay? CreateDelay(long seconds)
    {
        return seconds is < 0 or > int.MaxValue
            ? null
            : new TelegramRetryAfterDelay((int)seconds);
    }
}

internal readonly record struct TelegramRetryAfterDelay(int Seconds)
{
    public TimeSpan Value => TimeSpan.FromSeconds(Seconds);
}
