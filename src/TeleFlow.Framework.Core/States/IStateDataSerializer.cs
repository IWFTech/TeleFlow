namespace TeleFlow.Framework.States;

public interface IStateDataSerializer
{
    string Serialize<TValue>(TValue value);

    TValue? Deserialize<TValue>(string value);
}
