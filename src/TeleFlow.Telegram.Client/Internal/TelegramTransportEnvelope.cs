using System.Text.Json;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Owns a parsed Telegram Bot API response envelope for one request attempt.
/// The request executor uses it to decide between success, API error, retry-after, and decode failure paths.
/// </summary>
internal sealed class TelegramTransportEnvelope : IDisposable
{
    private readonly JsonDocument _document;

    public TelegramTransportEnvelope(
        JsonDocument document,
        bool ok,
        bool hasResult,
        JsonElement result,
        string? description,
        int? errorCode,
        ResponseParameters? responseParameters)
    {
        ArgumentNullException.ThrowIfNull(document);

        _document = document;
        Ok = ok;
        HasResult = hasResult;
        Result = result;
        Description = description;
        ErrorCode = errorCode;
        ResponseParameters = responseParameters;
    }

    public bool Ok { get; }

    public bool HasResult { get; }

    public JsonElement Result { get; }

    public string? Description { get; }

    public int? ErrorCode { get; }

    public ResponseParameters? ResponseParameters { get; }

    public void Dispose()
    {
        _document.Dispose();
    }
}
