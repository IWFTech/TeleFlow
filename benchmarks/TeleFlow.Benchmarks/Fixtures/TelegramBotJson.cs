using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TeleFlow.Benchmarks.Fixtures;

internal static class TelegramBotJson
{
    private static readonly JsonSerializerOptions SerializerOptions = JsonBotAPI.Options;

    public static Update DeserializeUpdate(string json)
    {
        return JsonSerializer.Deserialize<Update>(json, SerializerOptions)
               ?? throw new InvalidOperationException("The Telegram.Bot update fixture deserialized to null.");
    }
}
