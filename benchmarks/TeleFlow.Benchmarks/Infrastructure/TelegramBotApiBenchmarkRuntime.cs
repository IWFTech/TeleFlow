using Telegram.BotAPI;

namespace TeleFlow.Benchmarks.Infrastructure;

internal sealed class TelegramBotApiBenchmarkRuntime : IDisposable
{
    private readonly HttpClient _httpClient;

    private TelegramBotApiBenchmarkRuntime(
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

    public static TelegramBotApiBenchmarkRuntime Create(string responseJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseJson);

        var handler = new FixedTelegramBotHttpMessageHandler(responseJson);
        var httpClient = new HttpClient(handler, disposeHandler: true);
        var options = new TelegramBotClientOptions("123:benchmark", httpClient);
        var bot = new TelegramBotClient(options);

        return new TelegramBotApiBenchmarkRuntime(bot, httpClient, handler);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
