using TeleFlow.Telegram;

namespace TeleFlow.Benchmarks.Infrastructure;

internal static class TelegramTransportResponses
{
    public static string SendMessageOkJson()
    {
        return """
               {
                 "ok": true,
                 "result": {
                   "message_id": 5000001,
                   "date": 1710000100,
                   "chat": {
                     "id": 2000001,
                     "type": "private",
                     "first_name": "Benchmark"
                   },
                   "from": {
                     "id": 9000001,
                     "is_bot": true,
                     "first_name": "TeleFlow Bench Bot"
                   },
                   "text": "ok"
                 }
               }
               """;
    }

    public static TelegramTransportResponse SendMessageOk()
    {
        return new TelegramTransportResponse(200, SendMessageOkJson());
    }

    public static TelegramTransportResponse GetUpdatesOk(params string[] updateJson)
    {
        return new TelegramTransportResponse(200, GetUpdatesOkJson(updateJson));
    }

    public static string GetUpdatesOkJson(params string[] updateJson)
    {
        ArgumentNullException.ThrowIfNull(updateJson);

        if (updateJson.Length == 0)
        {
            return """
                   {
                     "ok": true,
                     "result": []
                   }
                   """;
        }

        return $$"""
                 {
                   "ok": true,
                   "result": [
                     {{string.Join(",\n    ", updateJson)}}
                   ]
                 }
                 """;
    }
}
