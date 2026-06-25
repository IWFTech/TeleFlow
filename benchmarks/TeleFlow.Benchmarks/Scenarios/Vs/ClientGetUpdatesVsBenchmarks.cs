using BenchmarkDotNet.Attributes;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.BotAPI.GettingUpdates;
using TelegramBotApiClient = Telegram.BotAPI.TelegramBotClient;
using TelegramBotApiUpdate = Telegram.BotAPI.GettingUpdates.Update;
using TelegramBotUpdate = Telegram.Bot.Types.Update;
using TeleFlow.Benchmarks.Fixtures;
using TeleFlow.Benchmarks.Infrastructure;
using TeleFlow.Telegram;
using TeleFlowUpdate = TeleFlow.Telegram.Schema.Types.Update;

namespace TeleFlow.Benchmarks.Scenarios.Vs;

[MemoryDiagnoser]
[BenchmarkCategory("vs", "client", "getUpdates")]
public class ClientGetUpdatesVsBenchmarks
{
    private static readonly string[] TeleFlowAllowedUpdates = ["message"];
    private static readonly UpdateType[] TelegramBotAllowedUpdates = [UpdateType.Message];

    private TeleFlowNativeClientBenchmarkRuntime _teleFlowRuntime = null!;
    private TelegramBotBenchmarkRuntime _telegramBotRuntime = null!;
    private TelegramBotApiBenchmarkRuntime _telegramBotApiRuntime = null!;
    private ITelegramClient _teleFlowBot = null!;
    private TelegramBotClient _telegramBot = null!;
    private TelegramBotApiClient _telegramBotApi = null!;

    [GlobalSetup]
    public void Setup()
    {
        var responseJson = TelegramTransportResponses.GetUpdatesOkJson(
            UpdateFixtureFiles.Read(UpdateFixture.MessageCommandStart));

        _teleFlowRuntime = TeleFlowNativeClientBenchmarkRuntime.Create(new TelegramTransportResponse(200, responseJson));
        _telegramBotRuntime = TelegramBotBenchmarkRuntime.Create(responseJson);
        _telegramBotApiRuntime = TelegramBotApiBenchmarkRuntime.Create(responseJson);

        _teleFlowBot = _teleFlowRuntime.Bot;
        _telegramBot = _telegramBotRuntime.Bot;
        _telegramBotApi = _telegramBotApiRuntime.Bot;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _teleFlowRuntime.Dispose();
        _telegramBotRuntime.Dispose();
        _telegramBotApiRuntime.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Task<IReadOnlyList<TeleFlowUpdate>> TeleFlow_GetUpdates()
    {
        return _teleFlowBot.GetUpdatesAsync(
            offset: 1000001,
            limit: 1,
            timeout: 0,
            allowedUpdates: TeleFlowAllowedUpdates);
    }

    [Benchmark]
    public Task<TelegramBotUpdate[]> TelegramBot_GetUpdates()
    {
        return _telegramBot.GetUpdates(
            offset: 1000001,
            limit: 1,
            timeout: 0,
            allowedUpdates: TelegramBotAllowedUpdates);
    }

    [Benchmark]
    public async Task<TelegramBotApiUpdate[]> TelegramBotApi_GetUpdates()
    {
        var updates = await _telegramBotApi
            .GetUpdatesAsync(
                offset: 1000001,
                limit: 1,
                timeout: 0,
                allowedUpdates: TeleFlowAllowedUpdates,
                cancellationToken: CancellationToken.None)
            .ConfigureAwait(false);

        return updates.ToArray();
    }
}
