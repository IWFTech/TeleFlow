using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public sealed class TelegramPolledUpdate
{
    private int _acknowledged;

    internal TelegramPolledUpdate(
        Update update,
        int batchIndex = 1,
        int batchCount = 1)
    {
        ArgumentNullException.ThrowIfNull(update);

        Update = update;
        BatchIndex = batchIndex;
        BatchCount = batchCount;
    }

    public Update Update { get; }

    internal int BatchIndex { get; }

    internal int BatchCount { get; }

    internal bool IsAcknowledged => Volatile.Read(ref _acknowledged) != 0;

    public ValueTask AcknowledgeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Exchange(ref _acknowledged, 1);
        return ValueTask.CompletedTask;
    }
}
