using System.Threading;

namespace TeleFlow.Telegram.Internal;

internal sealed class TelegramHandlerRequestTimingScope : IDisposable
{
    private static readonly AsyncLocal<TelegramHandlerRequestTimingScope?> CurrentSlot = new();

    private readonly TelegramHandlerRequestTimingScope? _previous;
    private readonly List<RequestInterval> _intervals = [];
    private bool _disposed;

    private TelegramHandlerRequestTimingScope(TelegramHandlerRequestTimingScope? previous)
    {
        _previous = previous;
    }

    public static TelegramHandlerRequestTimingScope Begin()
    {
        var scope = new TelegramHandlerRequestTimingScope(CurrentSlot.Value);
        CurrentSlot.Value = scope;
        return scope;
    }

    public static void Record(long startTimestamp, long endTimestamp)
    {
        if (endTimestamp < startTimestamp)
        {
            return;
        }

        CurrentSlot.Value?._intervals.Add(new RequestInterval(startTimestamp, endTimestamp));
    }

    public TelegramHandlerRequestTimingSummary CreateSummary(
        TimeProvider timeProvider,
        TimeSpan handlerElapsed)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var requestWait = CalculateMergedRequestWait(timeProvider);
        var logic = handlerElapsed - requestWait;

        if (logic < TimeSpan.Zero)
        {
            logic = TimeSpan.Zero;
        }

        return new TelegramHandlerRequestTimingSummary(
            _intervals.Count,
            requestWait.TotalMilliseconds,
            logic.TotalMilliseconds);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CurrentSlot.Value = _previous;
    }

    private TimeSpan CalculateMergedRequestWait(TimeProvider timeProvider)
    {
        if (_intervals.Count == 0)
        {
            return TimeSpan.Zero;
        }

        var ordered = _intervals
            .OrderBy(static interval => interval.StartTimestamp)
            .ToArray();
        var currentStart = ordered[0].StartTimestamp;
        var currentEnd = ordered[0].EndTimestamp;
        var total = TimeSpan.Zero;

        for (var index = 1; index < ordered.Length; index++)
        {
            var interval = ordered[index];

            if (interval.StartTimestamp <= currentEnd)
            {
                if (interval.EndTimestamp > currentEnd)
                {
                    currentEnd = interval.EndTimestamp;
                }

                continue;
            }

            total += timeProvider.GetElapsedTime(currentStart, currentEnd);
            currentStart = interval.StartTimestamp;
            currentEnd = interval.EndTimestamp;
        }

        total += timeProvider.GetElapsedTime(currentStart, currentEnd);
        return total;
    }

    private readonly record struct RequestInterval(long StartTimestamp, long EndTimestamp);
}

internal readonly record struct TelegramHandlerRequestTimingSummary(
    int RequestCount,
    double RequestElapsedMilliseconds,
    double HandlerLogicElapsedMilliseconds);
