using Microsoft.AspNetCore.Http;

namespace TeleFlow.Telegram.Webhooks;

public sealed class TelegramRawWebhookOptions
{
    public string? SecretToken { get; set; }

    public int InvalidPayloadStatusCode { get; set; } = StatusCodes.Status400BadRequest;

    public int SecretTokenFailureStatusCode { get; set; } = StatusCodes.Status401Unauthorized;
}
