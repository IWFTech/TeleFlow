using TeleFlow.Telegram.Schema.Abstractions;

namespace TeleFlow.Telegram.Internal;

internal sealed class UpdateScopedTelegramClient : ITelegramClient
{
    private readonly ITelegramClient _inner;
    private readonly CancellationToken _updateCancellationToken;

    public UpdateScopedTelegramClient(
        ITelegramClient inner,
        CancellationToken updateCancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
        _updateCancellationToken = updateCancellationToken;
    }

    public TelegramBotDefaults Defaults => _inner.Defaults;

    public TelegramDeepLinks DeepLinks => _inner.DeepLinks;

    public Task<TResult> SendAsync<TResult>(
        ITelegramApiMethod<TResult> method,
        CancellationToken cancellationToken = default)
    {
        var effectiveCancellationToken = cancellationToken.CanBeCanceled
            ? cancellationToken
            : _updateCancellationToken;

        return _inner.SendAsync(method, effectiveCancellationToken);
    }
}
