namespace TeleFlow.Telegram;

/// <summary>
/// Controls whether a transport stops or acknowledges a Telegram update that could not be decoded.
/// </summary>
public enum TelegramUpdateDecodeFailureDecision
{
    Stop,
    Skip
}
