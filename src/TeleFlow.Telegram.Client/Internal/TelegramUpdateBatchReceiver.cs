using TeleFlow.Telegram.Schema.Methods;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Executes the specialized getUpdates request that decodes each result element independently.
/// </summary>
internal sealed class TelegramUpdateBatchReceiver(ITelegramRequestExecutor executor) : ITelegramUpdateBatchReceiver
{
    private readonly ITelegramRequestExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

    public async Task<IReadOnlyList<TelegramUpdateDecodeResult>> ReceiveAsync(
        GetUpdates request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await _executor.ExecuteAsync(
            new TelegramUpdateBatchRequest(request),
            cancellationToken).ConfigureAwait(false);

        return response.Items;
    }
}
