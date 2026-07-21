namespace TeleFlow.Telegram.Internal;

internal sealed class TelegramRequestSender
{
    private readonly ITelegramTransport _transport;
    private readonly TelegramClientOptions _options;
    private readonly TelegramRequestContentBuilder _contentBuilder;

    public TelegramRequestSender(
        ITelegramTransport transport,
        TelegramClientOptions options,
        TelegramRequestContentBuilder contentBuilder)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(contentBuilder);

        _transport = transport;
        _options = options;
        _contentBuilder = contentBuilder;
    }

    public TelegramTransportRequest CreateRequest<TResponse>(
        ITelegramExecutableRequest<TResponse> request)
        where TResponse : ITelegramResponse
    {
        return new TelegramTransportRequest(
            request.MethodName,
            BuildMethodUri(request.MethodName),
            _contentBuilder.Build(request.Payload));
    }

    public async Task<TelegramTransportResponse> SendAsync(
        TelegramTransportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await _transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TelegramRequestException)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new TelegramNetworkException(
                $"Telegram request '{request.MethodName}' failed because the HTTP transport timed out or was interrupted.",
                exception,
                request.MethodName);
        }
        catch (Exception exception)
        {
            throw new TelegramNetworkException(
                $"Telegram request '{request.MethodName}' failed because the Telegram transport failed.",
                exception,
                request.MethodName);
        }
    }

    private Uri BuildMethodUri(string methodName)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var environmentPath = _options.Environment == TelegramBotApiEnvironment.Test
            ? "/test"
            : string.Empty;

        return new Uri($"{baseUrl}/bot{_options.Token}{environmentPath}/{methodName}", UriKind.Absolute);
    }
}
