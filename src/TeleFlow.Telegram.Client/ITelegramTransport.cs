namespace TeleFlow.Telegram;

public interface ITelegramTransport
{
    Task<TelegramTransportResponse> SendAsync(
        TelegramTransportRequest request,
        CancellationToken cancellationToken = default);
}
