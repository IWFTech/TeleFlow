using System.Text.Json;
using BenchmarkDotNet.Attributes;
using TeleFlow.Benchmarks.Fixtures;
using TeleFlow.Benchmarks.Infrastructure;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Responses;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Benchmarks.Scenarios.TeleFlow;

[MemoryDiagnoser]
[BenchmarkCategory("teleflow", "polling", "raw", "diagnostics")]
public class RawPollingDiagnosticsBenchmarks
{
    private const int ExpectedUpdateCount = 3;
    private static readonly string[] AllowedUpdates = ["message", "callback_query"];

    private readonly JsonSerializerOptions _serializerOptions =
        TelegramJsonOptions.CreateDefault().SerializerOptions;

    private string _responseJson = string.Empty;
    private TeleFlowNativeClientBenchmarkRuntime _runtime = null!;
    private ITelegramClient _bot = null!;
    private ITelegramLongPollingClient _longPolling = null!;
    private TelegramRawLongPollingOptions _options = null!;

    [GlobalSetup]
    public void Setup()
    {
        _responseJson = TelegramTransportResponses.GetUpdatesOkJson(
            UpdateFixtureFiles.Read(UpdateFixture.MessageCommandStart),
            UpdateFixtureFiles.Read(UpdateFixture.CallbackTicketTake),
            UpdateFixtureFiles.Read(UpdateFixture.MessageStateText));

        _runtime = TeleFlowNativeClientBenchmarkRuntime.Create(
            new TelegramTransportResponse(200, _responseJson),
            includeLongPolling: true);

        _bot = _runtime.Bot;
        _longPolling = _runtime.LongPolling;
        _options = new TelegramRawLongPollingOptions
        {
            Limit = ExpectedUpdateCount,
            TimeoutSeconds = 1,
            AllowedUpdates = AllowedUpdates
        };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _runtime.Dispose();
    }

    [Benchmark]
    public TelegramApiResponse<IReadOnlyList<Update>> DeserializeGetUpdatesResponse()
    {
        return JsonSerializer.Deserialize<TelegramApiResponse<IReadOnlyList<Update>>>(
                   _responseJson,
                   _serializerOptions)
               ?? throw new InvalidOperationException("The getUpdates response fixture deserialized to null.");
    }

    [Benchmark]
    public Task<IReadOnlyList<Update>> NativeClientGetUpdatesBatch()
    {
        return _bot.GetUpdatesAsync(
            offset: 1000001,
            limit: ExpectedUpdateCount,
            timeout: 0,
            allowedUpdates: AllowedUpdates);
    }

    [Benchmark]
    public async Task<int> StreamingLongPollingBatch()
    {
        var handled = 0;

        await foreach (var polledUpdate in _longPolling.GetUpdatesAsync(_options).ConfigureAwait(false))
        {
            handled++;
            await polledUpdate.AcknowledgeAsync().ConfigureAwait(false);

            if (handled == ExpectedUpdateCount)
            {
                break;
            }
        }

        return handled;
    }

    [Benchmark]
    public async Task<int> RunAsyncLongPollingBatch()
    {
        using var cancellation = new CancellationTokenSource();
        var state = new RawPollingState(cancellation);

        await _longPolling
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
                _options,
                cancellation.Token)
            .ConfigureAwait(false);

        return state.Handled;
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
