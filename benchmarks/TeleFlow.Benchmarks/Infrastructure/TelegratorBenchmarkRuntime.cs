using System.Collections.Concurrent;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using TeleFlow.Benchmarks.Handlers;
using TeleFlow.Benchmarks.Fixtures;
using Telegrator;
using Telegrator.Core.States;
using Telegrator.Filters;
using Telegrator.Handlers.Building;
using Telegrator.Mediation;
using Telegrator.Providers;

namespace TeleFlow.Benchmarks.Infrastructure;

internal sealed class TelegratorBenchmarkRuntime
{
    private readonly TelegramBotClient _bot;
    private readonly UpdateRouter _router;

    private TelegratorBenchmarkRuntime(TelegramBotClient bot, UpdateRouter router)
    {
        _bot = bot;
        _router = router;
    }

    public static async Task<TelegratorBenchmarkRuntime> CreateAsync(CancellationToken cancellationToken = default)
    {
        var options = new TelegratorOptions
        {
            Token = "123:benchmark",
            MaximumParallelWorkingHandlers = null
        };

        var handlers = new HandlersCollection(options);
        handlers.AddHandler<TelegratorBenchmarkCommandHandler>();
        var callbackBuilder = handlers.CreateCallbackQuery();
        callbackBuilder.AddTargetedFilter(
            static update => update.CallbackQuery!,
            new CallbackDataStartsWithFilter("ticket:", StringComparison.Ordinal));
        callbackBuilder.Build(TelegratorBenchmarkCallbackHandler.Execute);

        var provider = new HandlersProvider(handlers, options);
        var awaiting = new AwaitingProvider(options);
        var stateStorage = new TelegratorBenchmarkStateStorage();
        var botInfo = new TelegramBotInfo(new User
        {
            Id = 9000001,
            IsBot = true,
            FirstName = "Telegrator Bench Bot",
            Username = "telegrator_bench_bot"
        });

        var runtime = new TelegratorBenchmarkRuntime(
            new TelegramBotClient(options.Token),
            new UpdateRouter(provider, awaiting, stateStorage, options, botInfo));

        await runtime.VerifyCommandDispatchAsync(cancellationToken).ConfigureAwait(false);
        await runtime.VerifyCallbackDispatchAsync(cancellationToken).ConfigureAwait(false);

        return runtime;
    }

    public Task DispatchAsync(Update update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        return _router.HandleUpdateAsync(_bot, update, cancellationToken);
    }

    private async Task VerifyCommandDispatchAsync(CancellationToken cancellationToken)
    {
        TelegratorBenchmarkCommandHandler.Reset();

        await DispatchAsync(CreateUpdate(UpdateFixture.MessageCommandStart), cancellationToken)
            .ConfigureAwait(false);

        if (TelegratorBenchmarkCommandHandler.Calls != 1)
        {
            throw new InvalidOperationException("Telegrator command dispatch benchmark is invalid: the handler was not executed.");
        }

        TelegratorBenchmarkCommandHandler.Reset();
    }

    private async Task VerifyCallbackDispatchAsync(CancellationToken cancellationToken)
    {
        TelegratorBenchmarkCallbackHandler.Reset();

        await DispatchAsync(CreateUpdate(UpdateFixture.CallbackTicketTake), cancellationToken)
            .ConfigureAwait(false);

        if (TelegratorBenchmarkCallbackHandler.Calls != 1)
        {
            throw new InvalidOperationException("Telegrator callback dispatch benchmark is invalid: the handler was not executed.");
        }

        TelegratorBenchmarkCallbackHandler.Reset();
    }

    public static Update CreateUpdate(UpdateFixture fixture)
    {
        var json = UpdateFixtureFiles.Read(fixture);
        return JsonSerializer.Deserialize<Update>(json, JsonBotAPI.Options)
               ?? throw new InvalidOperationException("The Telegrator update fixture deserialized to null.");
    }
}

internal sealed class TelegratorBenchmarkStateStorage : IStateStorage
{
    private readonly ConcurrentDictionary<string, object?> _values = new();

    public Task SetAsync<T>(string key, T data, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _values[key] = data;
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return Task.FromResult(_values.TryGetValue(key, out var value) ? (T?)value : default);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _values.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
