using TeleFlow.Telegram.Schema.Abstractions;

namespace TeleFlow.Telegram.Internal;

internal sealed class TelegramClient : ITelegramClient
{
    private readonly ITelegramRequestExecutor _executor;

    public TelegramClient(
        ITelegramRequestExecutor executor,
        TelegramClientOptions options,
        TelegramDeepLinks deepLinks)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(deepLinks);

        _executor = executor;
        Defaults = options.Defaults;
        DeepLinks = deepLinks;
    }

    public TelegramBotDefaults Defaults { get; }

    public TelegramDeepLinks DeepLinks { get; }

    public async Task<TResult> SendAsync<TResult>(
        ITelegramApiMethod<TResult> method,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(method);

        var result = await _executor.ExecuteAsync(
            new SchemaTelegramRequest<TResult>(method),
            cancellationToken).ConfigureAwait(false);

        return result.Value;
    }
}
