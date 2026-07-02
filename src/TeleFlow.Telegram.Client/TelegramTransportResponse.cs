using System.Text;

namespace TeleFlow.Telegram;

/// <summary>
/// Represents one raw Telegram Bot API HTTP response returned by an <see cref="ITelegramTransport"/>.
/// The request executor parses the UTF-8 response body into a Telegram envelope before mapping it to typed API results or exceptions.
/// </summary>
public sealed class TelegramTransportResponse
{
    public TelegramTransportResponse(
        int statusCode,
        string body,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null)
        : this(statusCode, EncodeBody(body), headers, copyBody: false)
    {
    }

    public TelegramTransportResponse(
        int statusCode,
        byte[] body,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null)
        : this(statusCode, AsMemory(body), headers, copyBody: true)
    {
    }

    public TelegramTransportResponse(
        int statusCode,
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null)
        : this(statusCode, body, headers, copyBody: true)
    {
    }

    private TelegramTransportResponse(
        int statusCode,
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? headers,
        bool copyBody)
    {
        StatusCode = statusCode;
        Body = copyBody ? body.ToArray() : body;
        Headers = CopyHeaders(headers);
    }

    internal static TelegramTransportResponse FromOwnedBytes(
        int statusCode,
        byte[] body,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        return new TelegramTransportResponse(statusCode, body, headers, copyBody: false);
    }

    /// <summary>
    /// Gets the HTTP status code returned by Telegram.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the raw UTF-8 response body returned by Telegram.
    /// </summary>
    public ReadOnlyMemory<byte> Body { get; }

    /// <summary>
    /// Gets response headers copied from the underlying transport.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }

    /// <summary>
    /// Tries to read response header values by name using case-insensitive lookup.
    /// </summary>
    public bool TryGetHeaderValues(string name, out IReadOnlyList<string> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Headers.TryGetValue(name, out values!);
    }

    private static Dictionary<string, IReadOnlyList<string>> CopyHeaders(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? headers)
    {
        if (headers is null || headers.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        return headers.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static byte[] EncodeBody(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return Encoding.UTF8.GetBytes(body);
    }

    private static ReadOnlyMemory<byte> AsMemory(byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return body.AsMemory();
    }
}
