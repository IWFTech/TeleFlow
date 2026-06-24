using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

internal sealed class TelegramTransportEnvelope
{
    public required bool Ok { get; init; }

    public string? ResultJson { get; init; }

    public string? Description { get; init; }

    public int? ErrorCode { get; init; }

    public ResponseParameters? ResponseParameters { get; init; }
}
