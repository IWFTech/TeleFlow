namespace TeleFlow.Telegram;

/// <summary>
/// Sends raw Telegram Bot API transport requests and returns raw UTF-8 response bodies for the typed client pipeline.
/// Applications can replace this boundary for custom networking, proxies, tests, or controlled <see cref="HttpClient"/> ownership.
/// </summary>
public interface ITelegramTransport
{
    Task<TelegramTransportResponse> SendAsync(
        TelegramTransportRequest request,
        CancellationToken cancellationToken = default);
}
