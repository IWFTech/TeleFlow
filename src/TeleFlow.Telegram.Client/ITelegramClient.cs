using TeleFlow.Telegram.Schema.Abstractions;

namespace TeleFlow.Telegram;

public interface ITelegramClient
{
    TelegramBotDefaults Defaults { get; }

    TelegramDeepLinks DeepLinks { get; }

    Task<TResult> SendAsync<TResult>(
        ITelegramApiMethod<TResult> method,
        CancellationToken cancellationToken = default);
}
