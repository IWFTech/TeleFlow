using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;

namespace TeleFlow.Telegram;

public sealed class HttpClientTelegramTransport : ITelegramTransport, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    public HttpClientTelegramTransport(HttpClient httpClient)
        : this(httpClient, ownsHttpClient: false)
    {
    }

    private HttpClientTelegramTransport(HttpClient httpClient, bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
    }

    internal static HttpClientTelegramTransport CreateOwned(HttpClient httpClient)
    {
        return new HttpClientTelegramTransport(httpClient, ownsHttpClient: true);
    }

    public async Task<TelegramTransportResponse> SendAsync(
        TelegramTransportRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        using var message = new HttpRequestMessage(HttpMethod.Post, request.Uri)
        {
            Content = BuildContent(request.Content)
        };

        try
        {
            using var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return new TelegramTransportResponse(
                (int)response.StatusCode,
                body,
                CopyHeaders(response));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new TelegramNetworkException(
                $"Telegram request '{request.MethodName}' failed because the HTTP transport failed.",
                exception,
                request.MethodName,
                exception.StatusCode is null ? null : (int)exception.StatusCode);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static HttpContent BuildContent(TelegramTransportContent content)
    {
        return content switch
        {
            TelegramJsonTransportContent json => new StringContent(json.Json, Encoding.UTF8, "application/json"),
            TelegramMultipartTransportContent multipart => BuildMultipartContent(multipart),
            _ => throw new InvalidOperationException(
                $"Unsupported Telegram transport content type '{content.GetType().FullName}'.")
        };
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Part content ownership is transferred to MultipartFormDataContent, which is disposed by the request message.")]
    private static MultipartFormDataContent BuildMultipartContent(TelegramMultipartTransportContent multipart)
    {
        var content = new MultipartFormDataContent();

        foreach (var field in multipart.Fields)
        {
            content.Add(new StringContent(field.Value, Encoding.UTF8), field.Name);
        }

        foreach (var file in multipart.Files)
        {
            var streamContent = new StreamContent(new NonDisposingReadStream(file.Content));
            if (!string.IsNullOrWhiteSpace(file.ContentType))
            {
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            }

            content.Add(streamContent, file.Name, file.FileName);
        }

        return content;
    }

    private static Dictionary<string, IReadOnlyList<string>> CopyHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in response.Headers)
        {
            headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in response.Content.Headers)
        {
            headers[header.Key] = header.Value.ToArray();
        }

        return headers;
    }

    private sealed class NonDisposingReadStream : Stream
    {
        [SuppressMessage(
            "Usage",
            "CA2213:Disposable fields should be disposed",
            Justification = "This wrapper deliberately does not own the caller-provided InputFile stream.")]
        private readonly Stream _inner;

        public NonDisposingReadStream(Stream inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            _inner = inner;
        }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            return _inner.Read(buffer);
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return _inner.ReadAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            // Stream ownership stays with the caller-provided InputFile.
            base.Dispose(disposing);
        }
    }
}
