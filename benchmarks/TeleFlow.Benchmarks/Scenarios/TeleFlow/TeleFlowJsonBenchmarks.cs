using BenchmarkDotNet.Attributes;
using TeleFlow.Benchmarks.Fixtures;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Benchmarks.Scenarios.TeleFlow;

[MemoryDiagnoser]
[BenchmarkCategory("teleflow", "json")]
public class TeleFlowJsonBenchmarks
{
    private string _messageJson = string.Empty;
    private string _callbackJson = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        _messageJson = UpdateFixtureFiles.Read(UpdateFixture.MessageCommandStart);
        _callbackJson = UpdateFixtureFiles.Read(UpdateFixture.CallbackTicketTake);
    }

    [Benchmark(Baseline = true)]
    public Update DeserializeMessageUpdate()
    {
        return TeleFlowJson.DeserializeUpdate(_messageJson);
    }

    [Benchmark]
    public Update DeserializeCallbackUpdate()
    {
        return TeleFlowJson.DeserializeUpdate(_callbackJson);
    }
}
