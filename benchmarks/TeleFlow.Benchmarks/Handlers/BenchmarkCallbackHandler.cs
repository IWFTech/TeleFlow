using TeleFlow.Annotations;
using TeleFlow.Benchmarks.Infrastructure;
using TeleFlow.Telegram;

namespace TeleFlow.Benchmarks.Handlers;

internal sealed class BenchmarkCallbackHandler
{
    [Callback]
    [CallbackDataPrefix("ticket:")]
    public Task HandleAsync(CallbackQueryContext context, BenchmarkProbe probe)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(probe);

        probe.Record(context.Update.UpdateId);
        return Task.CompletedTask;
    }
}
