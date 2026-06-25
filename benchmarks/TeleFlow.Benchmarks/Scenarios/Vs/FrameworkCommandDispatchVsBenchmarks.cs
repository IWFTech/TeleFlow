using BenchmarkDotNet.Attributes;
using TelegramBotUpdate = Telegram.Bot.Types.Update;
using TeleFlow.Benchmarks.Fixtures;
using TeleFlow.Benchmarks.Infrastructure;
using TeleFlow.Telegram;

namespace TeleFlow.Benchmarks.Scenarios.Vs;

[MemoryDiagnoser]
[BenchmarkCategory("vs", "framework", "dispatch", "command")]
public class FrameworkCommandDispatchVsBenchmarks
{
    private TeleFlowBenchmarkRuntime _teleFlowRuntime = null!;
    private TelegratorBenchmarkRuntime _telegratorRuntime = null!;
    private TelegramUpdatePayload _teleFlowStartCommandPayload = null!;
    private TelegramBotUpdate _telegratorStartCommandUpdate = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _teleFlowRuntime = TeleFlowBenchmarkRuntime.Create();
        _telegratorRuntime = await TelegratorBenchmarkRuntime.CreateAsync().ConfigureAwait(false);

        var startCommandJson = UpdateFixtureFiles.Read(UpdateFixture.MessageCommandStart);
        _teleFlowStartCommandPayload = new TelegramUpdatePayload(TeleFlowJson.DeserializeUpdate(startCommandJson));
        _telegratorStartCommandUpdate = TelegratorBenchmarkRuntime.CreateUpdate(UpdateFixture.MessageCommandStart);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _teleFlowRuntime.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Task TeleFlow_CommandRoute()
    {
        return _teleFlowRuntime.DispatchAsync(_teleFlowStartCommandPayload);
    }

    [Benchmark]
    public Task Telegrator_CommandRoute()
    {
        return _telegratorRuntime.DispatchAsync(_telegratorStartCommandUpdate);
    }
}
