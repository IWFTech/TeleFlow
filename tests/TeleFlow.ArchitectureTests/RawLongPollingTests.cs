using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Methods;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.ArchitectureTests;

public sealed class RawLongPollingTests
{
    [Fact]
    public async Task RunAsync_ProcessesUpdatesSequentially_AndAdvancesOffsetAfterSuccess()
    {
        var telegramClient = new SequencedTelegramClient(
            new List<Update> { CreateMessageUpdate(1) },
            new List<Update> { CreateMessageUpdate(2) });
        var processedUpdates = new List<long>();
        using var cancellation = new CancellationTokenSource();
        var polling = CreatePollingClient(telegramClient);

        await polling.RunAsync(
            (update, _) =>
            {
                processedUpdates.Add(update.UpdateId);
                if (processedUpdates.Count == 2)
                {
                    cancellation.Cancel();
                }

                return Task.CompletedTask;
            },
            cancellationToken: cancellation.Token);

        Assert.Equal([1L, 2L], processedUpdates);
        Assert.Equal([null, 2L], telegramClient.GetUpdatesRequests.Select(static request => request.Offset).ToArray());
    }

    [Fact]
    public async Task RunAsync_BubblesHandlerException_AndDoesNotPollWithAdvancedOffset()
    {
        var telegramClient = new SequencedTelegramClient(new List<Update> { CreateMessageUpdate(1) });
        var polling = CreatePollingClient(telegramClient);
        var expected = new InvalidOperationException("handler failed");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            polling.RunAsync((_, _) => Task.FromException(expected)));

        Assert.Same(expected, exception);
        Assert.Equal([null], telegramClient.GetUpdatesRequests.Select(static request => request.Offset).ToArray());
    }

    [Fact]
    public async Task RunAsync_RetriesTransientGetUpdatesFailures_AndResetsBackoffAfterSuccess()
    {
        var timeProvider = new RecordingTimeProvider();
        var telegramClient = new SequencedTelegramClient(
            new TelegramNetworkException("network failed", methodName: "getUpdates"),
            new TelegramServerException("server failed", methodName: "getUpdates", httpStatusCode: 502),
            Array.Empty<Update>(),
            new TelegramDecodeException("decode failed", methodName: "getUpdates"),
            new List<Update> { CreateMessageUpdate(1) });
        using var cancellation = new CancellationTokenSource();
        var polling = CreatePollingClient(telegramClient, timeProvider);

        await polling.RunAsync(
            (update, _) =>
            {
                Assert.Equal(1, update.UpdateId);
                cancellation.Cancel();
                return Task.CompletedTask;
            },
            new TelegramRawLongPollingOptions
            {
                Backoff =
                {
                    MinDelay = TimeSpan.FromSeconds(1),
                    MaxDelay = TimeSpan.FromSeconds(10),
                    Factor = 2,
                    Jitter = 0
                }
            },
            cancellation.Token);

        Assert.Equal(
            [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)],
            timeProvider.Delays);
        Assert.Equal([null, null, null, null, null], telegramClient.GetUpdatesRequests.Select(static request => request.Offset).ToArray());
    }

    [Fact]
    public async Task GetUpdatesAsync_AdvancesOffsetOnlyAfterAcknowledgement()
    {
        var telegramClient = new SequencedTelegramClient(
            new List<Update> { CreateMessageUpdate(1) },
            new List<Update> { CreateMessageUpdate(2) });
        var polling = CreatePollingClient(telegramClient);

        await using var enumerator = polling.GetUpdatesAsync().GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(1, enumerator.Current.Update.UpdateId);
        Assert.Equal([null], telegramClient.GetUpdatesRequests.Select(static request => request.Offset).ToArray());

        await enumerator.Current.AcknowledgeAsync();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(2, enumerator.Current.Update.UpdateId);
        Assert.Equal([null, 2L], telegramClient.GetUpdatesRequests.Select(static request => request.Offset).ToArray());
    }

    [Fact]
    public async Task GetUpdatesAsync_FailsFastWhenPreviousUpdateWasNotAcknowledged()
    {
        var telegramClient = new SequencedTelegramClient(
            new List<Update> { CreateMessageUpdate(1) },
            new List<Update> { CreateMessageUpdate(2) });
        var polling = CreatePollingClient(telegramClient);

        await using var enumerator = polling.GetUpdatesAsync().GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await enumerator.MoveNextAsync().AsTask());

        Assert.Contains("AcknowledgeAsync", exception.Message, StringComparison.Ordinal);
        Assert.Equal([null], telegramClient.GetUpdatesRequests.Select(static request => request.Offset).ToArray());
    }

    [Fact]
    public async Task RunAsync_SendsRawAllowedUpdatesStrings()
    {
        var telegramClient = new SequencedTelegramClient(new List<Update> { CreateMessageUpdate(1) });
        using var cancellation = new CancellationTokenSource();
        var polling = CreatePollingClient(telegramClient);

        await polling.RunAsync(
            (_, _) =>
            {
                cancellation.Cancel();
                return Task.CompletedTask;
            },
            new TelegramRawLongPollingOptions
            {
                AllowedUpdates = ["message", "custom_update"]
            },
            cancellation.Token);

        Assert.Equal(["message", "custom_update"], telegramClient.GetUpdatesRequests.Single().AllowedUpdates);
    }

    [Fact]
    public async Task RunAsync_ValidatesRawAllowedUpdates()
    {
        var polling = CreatePollingClient(new SequencedTelegramClient(Array.Empty<Update>()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            polling.RunAsync(
                static (_, _) => Task.CompletedTask,
                new TelegramRawLongPollingOptions { AllowedUpdates = ["message", " "] }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            polling.RunAsync(
                static (_, _) => Task.CompletedTask,
                new TelegramRawLongPollingOptions { AllowedUpdates = ["message", "message"] }));
    }

    [Fact]
    public void AddTelegramLongPollingClient_RegistersRawPollingClient()
    {
        var services = new ServiceCollection();

        services.AddTelegramClient(options => options.Token = "test-token");
        services.AddTelegramLongPollingClient();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<TelegramLongPollingClient>(provider.GetRequiredService<ITelegramLongPollingClient>());
    }

    private static TelegramLongPollingClient CreatePollingClient(
        ITelegramClient telegramClient,
        TimeProvider? timeProvider = null)
    {
        return new TelegramLongPollingClient(
            telegramClient,
            timeProvider ?? TimeProvider.System,
            NullLoggerFactory.Instance);
    }

    private static Update CreateMessageUpdate(long updateId)
    {
        return new Update
        {
            UpdateId = updateId,
            Message = new Message
            {
                MessageId = 10,
                Date = 0,
                Chat = new Chat { Id = 100, Type = "private" },
                Text = "hello"
            }
        };
    }

    private sealed class SequencedTelegramClient(params object[] results) : ITelegramClient
    {
        private readonly Queue<object> _results = new(results);

        public TelegramBotDefaults Defaults { get; } = new();

        public TelegramDeepLinks DeepLinks { get; } =
            new("test_bot", new Base64UrlJsonDeepLinkPayloadSerializer());

        public List<GetUpdates> GetUpdatesRequests { get; } = [];

        public Task<TResult> SendAsync<TResult>(
            ITelegramApiMethod<TResult> method,
            CancellationToken cancellationToken = default)
        {
            if (method is GetUpdates getUpdates)
            {
                GetUpdatesRequests.Add(getUpdates);
            }

            if (_results.Count == 0)
            {
                throw new InvalidOperationException("No queued Telegram client results remain.");
            }

            var result = _results.Dequeue();

            return result is Exception exception
                ? Task.FromException<TResult>(exception)
                : Task.FromResult((TResult)result);
        }
    }

    private sealed class RecordingTimeProvider : TimeProvider
    {
        public List<TimeSpan> Delays { get; } = [];

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            Delays.Add(dueTime);
            ThreadPool.QueueUserWorkItem(static queuedState =>
            {
                var invocation = (TimerInvocation)queuedState!;
                invocation.Callback(invocation.State);
            }, new TimerInvocation(callback, state));

            return new NoOpTimer();
        }

        private sealed record TimerInvocation(TimerCallback Callback, object? State);

        private sealed class NoOpTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                return true;
            }

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}
