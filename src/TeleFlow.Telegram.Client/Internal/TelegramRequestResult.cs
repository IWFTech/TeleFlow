namespace TeleFlow.Telegram.Internal;

internal sealed class TelegramRequestResult<TResult> : ITelegramResponse
{
    public TelegramRequestResult(TResult value)
    {
        Value = value;
    }

    public TResult Value { get; }
}
