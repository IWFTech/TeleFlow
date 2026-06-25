using System.Net;
using System.Text;

namespace TeleFlow.Benchmarks.Infrastructure;

internal sealed class FixedTelegramBotHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseJson;
    private int _requestCount;

    public FixedTelegramBotHttpMessageHandler(string responseJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseJson);

        _responseJson = responseJson;
    }

    public int RequestCount => Volatile.Read(ref _requestCount);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        Interlocked.Increment(ref _requestCount);

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
        };

        return Task.FromResult(response);
    }
}
