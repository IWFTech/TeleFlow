using BenchmarkDotNet.Attributes;
using TeleFlow.Benchmarks.Infrastructure;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Benchmarks.Scenarios.TeleFlow;

[MemoryDiagnoser]
[BenchmarkCategory("teleflow", "client")]
public class TeleFlowClientBenchmarks
{
    private TeleFlowBenchmarkRuntime _runtime = null!;
    private ITelegramClient _bot = null!;
    private IntegerString _chatId = null!;

    [GlobalSetup]
    public void Setup()
    {
        _runtime = TeleFlowBenchmarkRuntime.Create();
        _bot = _runtime.Bot;
        _chatId = IntegerString.From(2000001);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _runtime.Dispose();
    }

    [Benchmark]
    public Task<Message> SendMessageThroughFakeTransport()
    {
        return _bot.SendMessageAsync(_chatId, "benchmark message");
    }
}
