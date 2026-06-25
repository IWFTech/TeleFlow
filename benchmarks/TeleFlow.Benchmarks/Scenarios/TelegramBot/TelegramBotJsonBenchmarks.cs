using BenchmarkDotNet.Attributes;
using TeleFlow.Benchmarks.Fixtures;
using Telegram.Bot.Types;

namespace TeleFlow.Benchmarks.Scenarios.TelegramBot;

[MemoryDiagnoser]
[BenchmarkCategory("telegram-bot", "json")]
public class TelegramBotJsonBenchmarks
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
        return TelegramBotJson.DeserializeUpdate(_messageJson);
    }

    [Benchmark]
    public Update DeserializeCallbackUpdate()
    {
        return TelegramBotJson.DeserializeUpdate(_callbackJson);
    }
}
