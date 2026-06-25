using System.Text.Json;
using TelegramBotApiUpdate = Telegram.BotAPI.GettingUpdates.Update;

namespace TeleFlow.Benchmarks.Fixtures;

internal static class TelegramBotApiJson
{
    public static TelegramBotApiUpdate DeserializeUpdate(string json)
    {
        return JsonSerializer.Deserialize<TelegramBotApiUpdate>(json)
               ?? throw new InvalidOperationException("The Telegram.BotAPI update fixture deserialized to null.");
    }
}
