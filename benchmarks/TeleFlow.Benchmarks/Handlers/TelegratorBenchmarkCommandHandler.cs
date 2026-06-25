using Telegram.Bot.Types;
using Telegrator;
using Telegrator.Annotations;
using Telegrator.Handlers;

namespace TeleFlow.Benchmarks.Handlers;

[CommandAlias("start")]
[CommandHandler]
internal sealed class TelegratorBenchmarkCommandHandler : CommandHandler
{
    private static int _calls;

    public static int Calls => Volatile.Read(ref _calls);

    public static void Reset()
    {
        Volatile.Write(ref _calls, 0);
    }

    public override Task<Result> Execute(IHandlerContainer<Message> container, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(container);

        Interlocked.Increment(ref _calls);
        return Task.FromResult(Ok);
    }
}
