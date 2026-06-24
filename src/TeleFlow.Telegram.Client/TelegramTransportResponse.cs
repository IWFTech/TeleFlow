namespace TeleFlow.Telegram;

public sealed class TelegramTransportResponse
{
    public TelegramTransportResponse(
        int statusCode,
        string body,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null)
    {
        ArgumentNullException.ThrowIfNull(body);

        StatusCode = statusCode;
        Body = body;
        Headers = CopyHeaders(headers);
    }

    public int StatusCode { get; }

    public string Body { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }

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
}
