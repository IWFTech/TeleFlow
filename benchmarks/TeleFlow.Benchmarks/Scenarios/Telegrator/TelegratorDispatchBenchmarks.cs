using BenchmarkDotNet.Attributes;
using Telegram.Bot.Types;
using TeleFlow.Benchmarks.Fixtures;
using TeleFlow.Benchmarks.Infrastructure;

namespace TeleFlow.Benchmarks.Scenarios.Telegrator;

[MemoryDiagnoser]
[BenchmarkCategory("telegrator", "dispatch")]
public class TelegratorDispatchBenchmarks
{
    private TelegratorBenchmarkRuntime _runtime = null!;
    private Update _startCommandUpdate = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _runtime = await TelegratorBenchmarkRuntime.CreateAsync().ConfigureAwait(false);
        _startCommandUpdate = TelegratorBenchmarkRuntime.CreateUpdate(UpdateFixture.MessageCommandStart);
    }

    [Benchmark]
    public Task CommandRoute()
    {
        return _runtime.DispatchAsync(_startCommandUpdate);
    }
}
