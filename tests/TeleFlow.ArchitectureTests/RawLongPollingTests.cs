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
            new TelegramNetworkException("network failed after recovery", methodName: "getUpdates"),
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
    public async Task RunAsync_DefaultPolicyStopsOnIndividualDecodeFailureWithoutRetry()
    {
        var transport = new QueueTelegramTransport(CreatePoisonBatchResponse());
        using var provider = CreateTransportProvider(transport);
        var polling = provider.GetRequiredService<ITelegramLongPollingClient>();
        var processedUpdates = new List<long>();

        var exception = await Assert.ThrowsAsync<TelegramUpdateDecodeException>(() =>
            polling.RunAsync((update, _) =>
            {
                processedUpdates.Add(update.UpdateId);
                return Task.CompletedTask;
            }));

        Assert.Equal(2, exception.UpdateId);
        Assert.Equal("$.message.date", exception.JsonPath);
        Assert.Equal([1L], processedUpdates);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task RunAsync_SkipPolicyProcessesRemainingUpdatesAndAdvancesOffset()
    {
        var transport = new QueueTelegramTransport(
            CreatePoisonBatchResponse(),
            CreateBatchResponse(CreateUpdateJson(4)));
        using var provider = CreateTransportProvider<SkippingDecodeFailurePolicy>(transport);
        var polling = provider.GetRequiredService<ITelegramLongPollingClient>();
        var failurePolicy = provider.GetRequiredService<ITelegramUpdateDecodeFailurePolicy>();
        var processedUpdates = new List<long>();
        using var cancellation = new CancellationTokenSource();

        await polling.RunAsync((update, _) =>
        {
            processedUpdates.Add(update.UpdateId);
            if (update.UpdateId == 4)
            {
                cancellation.Cancel();
            }

            return Task.CompletedTask;
        }, cancellationToken: cancellation.Token);

        var failure = Assert.Single(Assert.IsType<SkippingDecodeFailurePolicy>(failurePolicy).Failures);
        Assert.Equal(TelegramUpdateTransport.LongPolling, failure.Transport);
        Assert.Equal(2, failure.UpdateId);
        Assert.Contains("\"date\":\"invalid\"", failure.RawPayloadJson, StringComparison.Ordinal);
        Assert.Matches("^[0-9a-f]{64}$", failure.PayloadSha256);
        Assert.Equal([1L, 3L, 4L], processedUpdates);
        Assert.Equal(2, transport.Requests.Count);
        Assert.Contains(
            "\"offset\":4",
            Assert.IsType<TelegramJsonTransportContent>(transport.Requests[1].Content).Json,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_DoesNotAdvanceOffsetWhenDecodeFailureHandlerThrows()
    {
        var transport = new QueueTelegramTransport(CreatePoisonOnlyResponse());
        using var provider = CreateTransportProvider<ThrowingDecodeFailurePolicy>(transport);
        var polling = provider.GetRequiredService<ITelegramLongPollingClient>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            polling.RunAsync(static (_, _) => Task.CompletedTask));

        Assert.Equal("quarantine failed", exception.Message);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task RunAsync_DoesNotAdvanceOffsetWhenDecodeFailurePolicyCancels()
    {
        var transport = new QueueTelegramTransport(CreatePoisonOnlyResponse());
        using var provider = CreateTransportProvider<CancellingDecodeFailurePolicy>(transport);
        var polling = provider.GetRequiredService<ITelegramLongPollingClient>();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            polling.RunAsync(static (_, _) => Task.CompletedTask));

        Assert.Single(transport.Requests);
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

    [Fact]
    public void AddTelegramLongPollingClient_ProvidesFailClosedPolicyForManuallyRegisteredClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITelegramClient>(new SequencedTelegramClient(Array.Empty<Update>()));
        services.AddTelegramLongPollingClient();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ITelegramUpdateDecodeFailurePolicy>());
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

    private static ServiceProvider CreateTransportProvider(QueueTelegramTransport transport)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITelegramTransport>(transport);
        services.AddTelegramClient(options => options.Token = "test-token");
        services.AddTelegramLongPollingClient();
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateTransportProvider<TFailurePolicy>(QueueTelegramTransport transport)
        where TFailurePolicy : class, ITelegramUpdateDecodeFailurePolicy
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITelegramTransport>(transport);
        services.AddTelegramClient(options => options.Token = "test-token");
        services.AddTelegramUpdateDecodeFailurePolicy<TFailurePolicy>();
        services.AddTelegramLongPollingClient();
        return services.BuildServiceProvider();
    }

    private static TelegramTransportResponse CreatePoisonBatchResponse()
    {
        return CreateBatchResponse(
            CreateUpdateJson(1),
            CreateUpdateJson(2, "\"invalid\""),
            CreateUpdateJson(3));
    }

    private static TelegramTransportResponse CreatePoisonOnlyResponse()
    {
        return CreateBatchResponse(CreateUpdateJson(2, "\"invalid\""));
    }

    private static TelegramTransportResponse CreateBatchResponse(params string[] updates)
    {
        return new TelegramTransportResponse(
            200,
            $"{{\"ok\":true,\"result\":[{string.Join(',', updates)}]}}");
    }

    private static string CreateUpdateJson(long updateId, string date = "0")
    {
        return $"{{\"update_id\":{updateId},\"message\":{{\"message_id\":10,\"date\":{date},\"chat\":{{\"id\":100,\"type\":\"private\"}},\"text\":\"hello\"}}}}";
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

    private sealed class QueueTelegramTransport(params TelegramTransportResponse[] responses) : ITelegramTransport
    {
        private readonly Queue<TelegramTransportResponse> _responses = new(responses);

        public List<TelegramTransportRequest> Requests { get; } = [];

        public Task<TelegramTransportResponse> SendAsync(
            TelegramTransportRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class SkippingDecodeFailurePolicy : ITelegramUpdateDecodeFailurePolicy
    {
        public List<TelegramUpdateDecodeFailure> Failures { get; } = [];

        public ValueTask<TelegramUpdateDecodeFailureDecision> DecideAsync(
            TelegramUpdateDecodeFailure failure,
            CancellationToken cancellationToken = default)
        {
            Failures.Add(failure);
            return ValueTask.FromResult(TelegramUpdateDecodeFailureDecision.Skip);
        }
    }

    private sealed class ThrowingDecodeFailurePolicy : ITelegramUpdateDecodeFailurePolicy
    {
        public ValueTask<TelegramUpdateDecodeFailureDecision> DecideAsync(
            TelegramUpdateDecodeFailure failure,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<TelegramUpdateDecodeFailureDecision>(
                new InvalidOperationException("quarantine failed"));
        }
    }

    private sealed class CancellingDecodeFailurePolicy : ITelegramUpdateDecodeFailurePolicy
    {
        public ValueTask<TelegramUpdateDecodeFailureDecision> DecideAsync(
            TelegramUpdateDecodeFailure failure,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<TelegramUpdateDecodeFailureDecision>(
                new OperationCanceledException(cancellationToken));
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
