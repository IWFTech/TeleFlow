namespace TeleFlow.Telegram;

/// <summary>
/// Describes one syntactically valid Telegram update that could not be mapped to the installed schema.
/// Applications can persist the raw payload before explicitly allowing the transport to skip it.
/// </summary>
public sealed class TelegramUpdateDecodeFailure
{
    /// <summary>
    /// Creates evidence for one update schema failure without exposing the raw payload through <see cref="ToString"/>.
    /// </summary>
    public TelegramUpdateDecodeFailure(
        TelegramUpdateTransport transport,
        long updateId,
        string rawPayloadJson,
        string payloadSha256,
        TelegramUpdateDecodeException exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPayloadJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadSha256);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.UpdateId != updateId)
        {
            throw new ArgumentException("The decode exception belongs to a different Telegram update.", nameof(exception));
        }

        if (!string.Equals(exception.PayloadSha256, payloadSha256, StringComparison.Ordinal))
        {
            throw new ArgumentException("The decode exception contains a different payload fingerprint.", nameof(exception));
        }

        Transport = transport;
        UpdateId = updateId;
        RawPayloadJson = rawPayloadJson;
        PayloadSha256 = payloadSha256;
        Exception = exception;
    }

    /// <summary>Gets the transport that received the incompatible update.</summary>
    public TelegramUpdateTransport Transport { get; }

    /// <summary>Gets the Telegram update identifier used for acknowledgement ordering.</summary>
    public long UpdateId { get; }

    /// <summary>Gets the complete raw JSON update for durable quarantine.</summary>
    public string RawPayloadJson { get; }

    /// <summary>Gets the lowercase SHA-256 fingerprint of <see cref="RawPayloadJson"/>.</summary>
    public string PayloadSha256 { get; }

    /// <summary>Gets the structured schema decode exception.</summary>
    public TelegramUpdateDecodeException Exception { get; }

    public override string ToString()
    {
        return $"Telegram update {UpdateId} failed schema decoding on {Transport}; payload_sha256={PayloadSha256}.";
    }
}
