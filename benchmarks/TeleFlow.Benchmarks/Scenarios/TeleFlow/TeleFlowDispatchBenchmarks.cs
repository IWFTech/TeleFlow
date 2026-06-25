using BenchmarkDotNet.Attributes;
using TeleFlow.Benchmarks.Fixtures;
using TeleFlow.Benchmarks.Infrastructure;
using TeleFlow.Telegram;

namespace TeleFlow.Benchmarks.Scenarios.TeleFlow;

[MemoryDiagnoser]
[BenchmarkCategory("teleflow", "dispatch")]
public class TeleFlowDispatchBenchmarks
{
    private TeleFlowBenchmarkRuntime _runtime = null!;
    private TelegramUpdatePayload _startCommandPayload = null!;
    private TelegramUpdatePayload _callbackPayload = null!;
    private TelegramUpdatePayload _statePayload = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _runtime = TeleFlowBenchmarkRuntime.Create();
        _startCommandPayload = CreatePayload(UpdateFixture.MessageCommandStart);
        _callbackPayload = CreatePayload(UpdateFixture.CallbackTicketTake);
        _statePayload = CreatePayload(UpdateFixture.MessageStateText);

        await _runtime
            .SetBenchmarkStateAsync(BenchmarkStates.AwaitingName)
            .ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _runtime.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Task CommandRoute()
    {
        return _runtime.DispatchAsync(_startCommandPayload);
    }

    [Benchmark]
    public Task CallbackPrefixRoute()
    {
        return _runtime.DispatchAsync(_callbackPayload);
    }

    [Benchmark]
    public Task StateRoute()
    {
        return _runtime.DispatchAsync(_statePayload);
    }

    private static TelegramUpdatePayload CreatePayload(UpdateFixture fixture)
    {
        var json = UpdateFixtureFiles.Read(fixture);
        return new TelegramUpdatePayload(TeleFlowJson.DeserializeUpdate(json));
    }
}
