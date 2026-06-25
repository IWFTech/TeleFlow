using BenchmarkDotNet.Attributes;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.BotAPI.GettingUpdates;
using TelegramBotApiClient = Telegram.BotAPI.TelegramBotClient;
using TeleFlow.Benchmarks.Fixtures;
using TeleFlow.Benchmarks.Infrastructure;
using TeleFlow.Telegram;

namespace TeleFlow.Benchmarks.Scenarios.Vs;

[MemoryDiagnoser]
[BenchmarkCategory("vs", "polling", "raw")]
public class RawPollingBatchVsBenchmarks
{
    private const int ExpectedUpdateCount = 3;
    private static readonly string[] TeleFlowAllowedUpdates = ["message", "callback_query"];
    private static readonly UpdateType[] TelegramBotAllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery];

    private TeleFlowNativeClientBenchmarkRuntime _teleFlowRuntime = null!;
    private TelegramBotBenchmarkRuntime _telegramBotRuntime = null!;
    private TelegramBotApiBenchmarkRuntime _telegramBotApiRuntime = null!;
    private ITelegramLongPollingClient _teleFlowLongPolling = null!;
    private TelegramBotClient _telegramBot = null!;
    private TelegramBotApiClient _telegramBotApi = null!;
    private TelegramRawLongPollingOptions _teleFlowOptions = null!;

    [GlobalSetup]
    public void Setup()
    {
        var responseJson = TelegramTransportResponses.GetUpdatesOkJson(
            UpdateFixtureFiles.Read(UpdateFixture.MessageCommandStart),
            UpdateFixtureFiles.Read(UpdateFixture.CallbackTicketTake),
            UpdateFixtureFiles.Read(UpdateFixture.MessageStateText));

        _teleFlowRuntime = TeleFlowNativeClientBenchmarkRuntime.Create(
            new TelegramTransportResponse(200, responseJson),
            includeLongPolling: true);
        _telegramBotRuntime = TelegramBotBenchmarkRuntime.Create(responseJson);
        _telegramBotApiRuntime = TelegramBotApiBenchmarkRuntime.Create(responseJson);

        _teleFlowLongPolling = _teleFlowRuntime.LongPolling;
        _telegramBot = _telegramBotRuntime.Bot;
        _telegramBotApi = _telegramBotApiRuntime.Bot;
        _teleFlowOptions = new TelegramRawLongPollingOptions
        {
            Limit = ExpectedUpdateCount,
            TimeoutSeconds = 1,
            AllowedUpdates = TeleFlowAllowedUpdates
        };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _teleFlowRuntime.Dispose();
        _telegramBotRuntime.Dispose();
        _telegramBotApiRuntime.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> TeleFlow_RawPollingBatch()
    {
        using var cancellation = new CancellationTokenSource();
        var state = new RawPollingState(cancellation);

        await _teleFlowLongPolling
            .RunAsync(
                (_, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    state.Handled++;
                    if (state.Handled == ExpectedUpdateCount)
                    {
                        state.Cancellation.Cancel();
                    }

                    return Task.CompletedTask;
                },
                _teleFlowOptions,
                cancellation.Token)
            .ConfigureAwait(false);

        return state.Handled;
    }

    [Benchmark]
    public async Task<int> TelegramBot_RawPollingBatch()
    {
        var handled = 0;
        var offset = 1000001;

        using var cancellation = new CancellationTokenSource();

        while (!cancellation.IsCancellationRequested)
        {
            var updates = await _telegramBot
                .GetUpdates(
                    offset: offset,
                    limit: ExpectedUpdateCount,
                    timeout: 1,
                    allowedUpdates: TelegramBotAllowedUpdates,
                    cancellationToken: cancellation.Token)
                .ConfigureAwait(false);

            for (var index = 0; index < updates.Length; index++)
            {
                cancellation.Token.ThrowIfCancellationRequested();

                handled++;
                offset = updates[index].Id + 1;

                if (handled == ExpectedUpdateCount)
                {
                    cancellation.Cancel();
                    break;
                }
            }
        }

        return handled;
    }

    [Benchmark]
    public async Task<int> TelegramBotApi_RawPollingBatch()
    {
        var handled = 0;
        var offset = 1000001;

        using var cancellation = new CancellationTokenSource();

        while (!cancellation.IsCancellationRequested)
        {
            var updates = await _telegramBotApi
                .GetUpdatesAsync(
                    offset: offset,
                    limit: ExpectedUpdateCount,
                    timeout: 1,
                    allowedUpdates: TeleFlowAllowedUpdates,
                    cancellationToken: cancellation.Token)
                .ConfigureAwait(false);

            foreach (var update in updates)
            {
                cancellation.Token.ThrowIfCancellationRequested();

                handled++;
                offset = update.UpdateId + 1;

                if (handled == ExpectedUpdateCount)
                {
                    cancellation.Cancel();
                    break;
                }
            }
        }

        return handled;
    }

    private sealed class RawPollingState
    {
        public RawPollingState(CancellationTokenSource cancellation)
        {
            Cancellation = cancellation;
        }

        public CancellationTokenSource Cancellation { get; }

        public int Handled { get; set; }
    }
}
