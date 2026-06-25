using System.Threading;

namespace TeleFlow.Benchmarks.Infrastructure;

internal sealed class BenchmarkProbe
{
    private long _value;

    public void Record(long value)
    {
        Volatile.Write(ref _value, value);
    }

    public long Value => Volatile.Read(ref _value);
}
