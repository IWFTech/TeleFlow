using System.Threading;
using TeleFlow.Telegram;

namespace TeleFlow.Benchmarks.Infrastructure;

internal sealed class FixedTelegramTransport : ITelegramTransport
{
    private readonly TelegramTransportResponse _response;
    private int _requestCount;

    public FixedTelegramTransport(TelegramTransportResponse response)
    {
        _response = response ?? throw new ArgumentNullException(nameof(response));
    }

    public int RequestCount => Volatile.Read(ref _requestCount);

    public Task<TelegramTransportResponse> SendAsync(
        TelegramTransportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        Interlocked.Increment(ref _requestCount);
        return Task.FromResult(_response);
    }
}
