namespace TeleFlow.Telegram;

public interface ITelegramRequestExecutor
{
    Task<TResponse> ExecuteAsync<TResponse>(
        ITelegramRequest<TResponse> request,
        CancellationToken cancellationToken = default)
        where TResponse : ITelegramResponse;
}
