namespace TeleFlow.Telegram;

public sealed class ChatActionLease : IAsyncDisposable
{
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly CancellationTokenSource _linkedCancellation;
    private readonly CancellationToken _linkedToken;
    private readonly Task _repeatTask;
    private int _disposed;

    internal ChatActionLease(
        ITelegramClient bot,
        TelegramChatActionTarget target,
        ChatAction action,
        TimeProvider timeProvider,
        TimeSpan repeatDelay,
        CancellationToken cancellationToken)
    {
        _linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _disposeCancellation.Token);
        _linkedToken = _linkedCancellation.Token;
        _repeatTask = RepeatAsync(bot, target, action, timeProvider, repeatDelay, _linkedToken);
    }

    public async ValueTask DisposeAsync()
    {
        var shouldDispose = Interlocked.Exchange(ref _disposed, 1) == 0;
        if (shouldDispose)
        {
            await _disposeCancellation.CancelAsync().ConfigureAwait(false);
        }

        try
        {
            await _repeatTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_linkedToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (shouldDispose)
            {
                _linkedCancellation.Dispose();
                _disposeCancellation.Dispose();
            }
        }
    }

    private static async Task RepeatAsync(
        ITelegramClient bot,
        TelegramChatActionTarget target,
        ChatAction action,
        TimeProvider timeProvider,
        TimeSpan repeatDelay,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(repeatDelay, timeProvider, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await ChatActions.SendChatActionAsync(bot, target, action, cancellationToken).ConfigureAwait(false);
        }
    }
}
