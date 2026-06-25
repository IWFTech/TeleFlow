using BenchmarkDotNet.Attributes;
using TelegramBotUpdate = Telegram.Bot.Types.Update;
using TeleFlow.Benchmarks.Fixtures;
using TeleFlow.Benchmarks.Infrastructure;
using TeleFlow.Telegram;

namespace TeleFlow.Benchmarks.Scenarios.Vs;

[MemoryDiagnoser]
[BenchmarkCategory("vs", "framework", "dispatch", "callback")]
public class FrameworkCallbackDispatchVsBenchmarks
{
    private TeleFlowBenchmarkRuntime _teleFlowRuntime = null!;
    private TelegratorBenchmarkRuntime _telegratorRuntime = null!;
    private TelegramUpdatePayload _teleFlowCallbackPayload = null!;
    private TelegramBotUpdate _telegratorCallbackUpdate = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _teleFlowRuntime = TeleFlowBenchmarkRuntime.Create();
        _telegratorRuntime = await TelegratorBenchmarkRuntime.CreateAsync().ConfigureAwait(false);

        var callbackJson = UpdateFixtureFiles.Read(UpdateFixture.CallbackTicketTake);
        _teleFlowCallbackPayload = new TelegramUpdatePayload(TeleFlowJson.DeserializeUpdate(callbackJson));
        _telegratorCallbackUpdate = TelegratorBenchmarkRuntime.CreateUpdate(UpdateFixture.CallbackTicketTake);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _teleFlowRuntime.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Task TeleFlow_CallbackRoute()
    {
        return _teleFlowRuntime.DispatchAsync(_teleFlowCallbackPayload);
    }

    [Benchmark]
    public Task Telegrator_CallbackRoute()
    {
        return _telegratorRuntime.DispatchAsync(_telegratorCallbackUpdate);
    }
}
