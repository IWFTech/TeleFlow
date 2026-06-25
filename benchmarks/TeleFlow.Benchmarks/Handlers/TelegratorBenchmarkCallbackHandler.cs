using Telegram.Bot.Types;
using Telegrator;
using Telegrator.Handlers;

namespace TeleFlow.Benchmarks.Handlers;

internal static class TelegratorBenchmarkCallbackHandler
{
    private static int _calls;

    public static int Calls => Volatile.Read(ref _calls);

    public static void Reset()
    {
        Volatile.Write(ref _calls, 0);
    }

    public static Task<Result> Execute(
        IHandlerContainer<CallbackQuery> container,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(container);
        cancellationToken.ThrowIfCancellationRequested();

        Interlocked.Increment(ref _calls);
        return Task.FromResult(Result.Ok());
    }
}
