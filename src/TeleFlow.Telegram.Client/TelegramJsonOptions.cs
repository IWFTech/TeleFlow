using System.Text.Json;

namespace TeleFlow.Telegram;

public sealed class TelegramJsonOptions
{
    public TelegramJsonOptions(JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(serializerOptions);
        SerializerOptions = new JsonSerializerOptions(serializerOptions);
    }

    public JsonSerializerOptions SerializerOptions { get; }

    public static TelegramJsonOptions CreateDefault()
    {
        return new TelegramJsonOptions(CreateDefaultSerializerOptions());
    }

    internal static JsonSerializerOptions CreateDefaultSerializerOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false
        };
    }
}
