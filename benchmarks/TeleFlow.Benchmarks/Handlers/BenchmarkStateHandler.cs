using TeleFlow.Annotations;
using TeleFlow.Benchmarks.Infrastructure;
using TeleFlow.Telegram;

namespace TeleFlow.Benchmarks.Handlers;

internal sealed class BenchmarkStateHandler
{
    [Message]
    [HasText]
    [State(BenchmarkStates.AwaitingName)]
    public Task HandleAsync(MessageContext context, BenchmarkProbe probe)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(probe);

        probe.Record(context.Update.UpdateId);
        return Task.CompletedTask;
    }
}
