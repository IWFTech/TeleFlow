using System.Diagnostics.CodeAnalysis;
using TeleFlow.Core.Updates;

namespace TeleFlow.Telegram.Webhooks.Internal;

[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "The type is instantiated by dependency injection through AddWebhook.")]
internal sealed class TelegramWebhookUpdateSource : IUpdateSource
{
    public async Task StartAsync(
        Func<IUpdatePayload, CancellationToken, Task> updateHandler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updateHandler);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
