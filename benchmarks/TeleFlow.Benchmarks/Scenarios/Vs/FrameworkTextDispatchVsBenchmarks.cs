using BenchmarkDotNet.Attributes;
using TeleFlow.Benchmarks.Fixtures;
using TeleFlow.Benchmarks.Infrastructure;
using TeleFlow.Telegram;

namespace TeleFlow.Benchmarks.Scenarios.Vs;

[MemoryDiagnoser]
[BenchmarkCategory("vs", "framework", "dispatch", "text")]
public class FrameworkTextDispatchVsBenchmarks
{
    private TeleFlowBenchmarkRuntime _teleFlowRuntime = null!;
    private TelegramBotBaseBenchmarkRuntime _telegramBotBaseRuntime = null!;
    private TelegramUpdatePayload _teleFlowStatePayload = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _teleFlowRuntime = TeleFlowBenchmarkRuntime.Create();
        _telegramBotBaseRuntime = await TelegramBotBaseBenchmarkRuntime
            .CreateAsync()
            .ConfigureAwait(false);

        var stateMessageJson = UpdateFixtureFiles.Read(UpdateFixture.MessageStateText);
        _teleFlowStatePayload = new TelegramUpdatePayload(TeleFlowJson.DeserializeUpdate(stateMessageJson));

        await _teleFlowRuntime
            .SetBenchmarkStateAsync(BenchmarkStates.AwaitingName)
            .ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _teleFlowRuntime.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Task TeleFlow_StateTextRoute()
    {
        return _teleFlowRuntime.DispatchAsync(_teleFlowStatePayload);
    }

    [Benchmark]
    public Task<int> TelegramBotBase_MinimalMessageLoop()
    {
        return _telegramBotBaseRuntime.DispatchTextMessageAsync();
    }
}
