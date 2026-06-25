using Telegram.Bot;

namespace TeleFlow.Benchmarks.Infrastructure;

internal sealed class TelegramBotBenchmarkRuntime : IDisposable
{
    private readonly HttpClient _httpClient;

    private TelegramBotBenchmarkRuntime(
        TelegramBotClient bot,
        HttpClient httpClient,
        FixedTelegramBotHttpMessageHandler handler)
    {
        Bot = bot;
        _httpClient = httpClient;
        Handler = handler;
    }

    public TelegramBotClient Bot { get; }

    public FixedTelegramBotHttpMessageHandler Handler { get; }

    public static TelegramBotBenchmarkRuntime Create(string responseJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseJson);

        var handler = new FixedTelegramBotHttpMessageHandler(responseJson);
        var httpClient = new HttpClient(handler, disposeHandler: true);
        var bot = new TelegramBotClient("123:benchmark", httpClient);

        return new TelegramBotBenchmarkRuntime(bot, httpClient, handler);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
