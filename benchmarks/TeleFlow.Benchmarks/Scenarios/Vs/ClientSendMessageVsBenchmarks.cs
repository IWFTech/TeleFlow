using BenchmarkDotNet.Attributes;
using Telegram.Bot;
using Telegram.BotAPI.AvailableMethods;
using TelegramBotApiClient = Telegram.BotAPI.TelegramBotClient;
using TelegramBotApiMessage = Telegram.BotAPI.AvailableTypes.Message;
using TelegramBotMessage = Telegram.Bot.Types.Message;
using TelegramBotChatId = Telegram.Bot.Types.ChatId;
using TeleFlow.Benchmarks.Infrastructure;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlowMessage = TeleFlow.Telegram.Schema.Types.Message;

namespace TeleFlow.Benchmarks.Scenarios.Vs;

[MemoryDiagnoser]
[BenchmarkCategory("vs", "client", "sendMessage")]
public class ClientSendMessageVsBenchmarks
{
    private TeleFlowNativeClientBenchmarkRuntime _teleFlowRuntime = null!;
    private TelegramBotBenchmarkRuntime _telegramBotRuntime = null!;
    private TelegramBotApiBenchmarkRuntime _telegramBotApiRuntime = null!;
    private ITelegramClient _teleFlowBot = null!;
    private TelegramBotClient _telegramBot = null!;
    private TelegramBotApiClient _telegramBotApi = null!;
    private IntegerString _teleFlowChatId = null!;
    private TelegramBotChatId _telegramBotChatId = null!;
    private SendMessageArgs _telegramBotApiSendMessage = null!;

    [GlobalSetup]
    public void Setup()
    {
        _teleFlowRuntime = TeleFlowNativeClientBenchmarkRuntime.Create(TelegramTransportResponses.SendMessageOk());
        _telegramBotRuntime = TelegramBotBenchmarkRuntime.Create(TelegramTransportResponses.SendMessageOkJson());
        _telegramBotApiRuntime = TelegramBotApiBenchmarkRuntime.Create(TelegramTransportResponses.SendMessageOkJson());

        _teleFlowBot = _teleFlowRuntime.Bot;
        _telegramBot = _telegramBotRuntime.Bot;
        _telegramBotApi = _telegramBotApiRuntime.Bot;
        _teleFlowChatId = IntegerString.From(2000001);
        _telegramBotChatId = new TelegramBotChatId(2000001);
        _telegramBotApiSendMessage = new SendMessageArgs(2000001, "benchmark message");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _teleFlowRuntime.Dispose();
        _telegramBotRuntime.Dispose();
        _telegramBotApiRuntime.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Task<TeleFlowMessage> TeleFlow_SendMessage()
    {
        return _teleFlowBot.SendMessageAsync(_teleFlowChatId, "benchmark message");
    }

    [Benchmark]
    public Task<TelegramBotMessage> TelegramBot_SendMessage()
    {
        return _telegramBot.SendMessage(_telegramBotChatId, "benchmark message");
    }

    [Benchmark]
    public Task<TelegramBotApiMessage> TelegramBotApi_SendMessage()
    {
        return _telegramBotApi.SendMessageAsync(_telegramBotApiSendMessage, CancellationToken.None);
    }
}
