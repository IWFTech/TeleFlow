using System.Text.Json;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Benchmarks.Fixtures;

internal static class TeleFlowJson
{
    private static readonly JsonSerializerOptions SerializerOptions =
        TelegramJsonOptions.CreateDefault().SerializerOptions;

    public static Update DeserializeUpdate(string json)
    {
        return JsonSerializer.Deserialize<Update>(json, SerializerOptions)
               ?? throw new InvalidOperationException("The TeleFlow update fixture deserialized to null.");
    }
}
