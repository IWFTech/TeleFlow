using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public interface ITelegramLongPollingClient
{
    Task RunAsync(
        Func<Update, CancellationToken, Task> updateHandler,
        TelegramRawLongPollingOptions? options = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<TelegramPolledUpdate> GetUpdatesAsync(
        TelegramRawLongPollingOptions? options = null,
        CancellationToken cancellationToken = default);
}
