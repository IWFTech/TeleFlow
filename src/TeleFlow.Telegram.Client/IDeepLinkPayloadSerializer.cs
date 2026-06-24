namespace TeleFlow.Telegram;

public interface IDeepLinkPayloadSerializer
{
    string Serialize<TPayload>(TPayload payload);

    TPayload Deserialize<TPayload>(string payload);
}
