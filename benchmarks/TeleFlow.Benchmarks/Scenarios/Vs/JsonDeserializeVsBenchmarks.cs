using BenchmarkDotNet.Attributes;
using TelegramBotApiUpdate = Telegram.BotAPI.GettingUpdates.Update;
using TelegramBotUpdate = Telegram.Bot.Types.Update;
using TeleFlow.Benchmarks.Fixtures;
using TeleFlowUpdate = TeleFlow.Telegram.Schema.Types.Update;

namespace TeleFlow.Benchmarks.Scenarios.Vs;

[MemoryDiagnoser]
[BenchmarkCategory("vs", "json", "deserialize")]
public class JsonDeserializeVsBenchmarks
{
    private string _json = null!;

    [Params(
        UpdateFixture.MessageCommandStart,
        UpdateFixture.CallbackTicketTake,
        UpdateFixture.MessageStateText)]
    public UpdateFixture Fixture { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _json = UpdateFixtureFiles.Read(Fixture);
    }

    [Benchmark(Baseline = true)]
    public TeleFlowUpdate TeleFlow_DeserializeUpdate()
    {
        return TeleFlowJson.DeserializeUpdate(_json);
    }

    [Benchmark]
    public TelegramBotUpdate TelegramBot_DeserializeUpdate()
    {
        return TelegramBotJson.DeserializeUpdate(_json);
    }

    [Benchmark]
    public TelegramBotApiUpdate TelegramBotApi_DeserializeUpdate()
    {
        return TelegramBotApiJson.DeserializeUpdate(_json);
    }
}
