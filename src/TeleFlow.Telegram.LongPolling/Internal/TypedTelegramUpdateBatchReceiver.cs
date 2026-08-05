using TeleFlow.Telegram.Schema.Methods;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Preserves the public direct-construction path by adapting an arbitrary Telegram client to successful update batches.
/// The dependency-injection path uses the raw per-update receiver instead.
/// </summary>
internal sealed class TypedTelegramUpdateBatchReceiver(ITelegramClient client) : ITelegramUpdateBatchReceiver
{
    private readonly ITelegramClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<IReadOnlyList<TelegramUpdateDecodeResult>> ReceiveAsync(
        GetUpdates request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var updates = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return updates.Select(TelegramUpdateDecodeResult.Success).ToArray();
    }
}
