using System.Diagnostics.CodeAnalysis;

namespace TeleFlow.Telegram.Internal;

internal sealed class TelegramRawLongPollingBackoff
{
    private readonly TelegramRawLongPollingBackoffOptions _options;
    private int _attempt;

    public TelegramRawLongPollingBackoff(TelegramRawLongPollingBackoffOptions options)
    {
        ValidateOptions(options);
        _options = options;
    }

    public static void ValidateOptions(TelegramRawLongPollingBackoffOptions? options)
    {
        if (options is null)
        {
            throw new InvalidOperationException("Raw long polling backoff options must be configured.");
        }

        if (options.MinDelay < TimeSpan.Zero)
        {
            throw new InvalidOperationException("Raw long polling backoff minimum delay must not be negative.");
        }

        if (options.MaxDelay < TimeSpan.Zero)
        {
            throw new InvalidOperationException("Raw long polling backoff maximum delay must not be negative.");
        }

        if (options.MaxDelay < options.MinDelay)
        {
            throw new InvalidOperationException("Raw long polling backoff maximum delay must be greater than or equal to minimum delay.");
        }

        if (options.Factor < 1)
        {
            throw new InvalidOperationException("Raw long polling backoff factor must be greater than or equal to one.");
        }

        if (options.Jitter is < 0 or > 1)
        {
            throw new InvalidOperationException("Raw long polling backoff jitter must be between zero and one.");
        }
    }

    public TimeSpan NextDelay()
    {
        if (!_options.Enabled)
        {
            return TimeSpan.Zero;
        }

        var minDelayMilliseconds = _options.MinDelay.TotalMilliseconds;
        var maxDelayMilliseconds = _options.MaxDelay.TotalMilliseconds;
        var delayMilliseconds = minDelayMilliseconds * Math.Pow(_options.Factor, _attempt);

        if (_options.Jitter > 0)
        {
            var jitterMultiplier = GetJitterMultiplier() * _options.Jitter;
            delayMilliseconds += delayMilliseconds * jitterMultiplier;
        }

        _attempt++;
        return TimeSpan.FromMilliseconds(Math.Min(delayMilliseconds, maxDelayMilliseconds));
    }

    public void Reset()
    {
        _attempt = 0;
    }

    [SuppressMessage(
        "Security",
        "CA5394:Do not use insecure randomness",
        Justification = "Backoff jitter is non-security timing noise and does not require cryptographic randomness.")]
    private static double GetJitterMultiplier()
    {
        return Random.Shared.NextDouble();
    }
}
