using Microsoft.Extensions.Hosting;
using TeleFlow.Telegram.Internal;

namespace TeleFlow.Telegram.Webhooks.Internal;

/// <summary>
/// Resolves the current bot identity before the ASP.NET Core host starts accepting Telegram webhook updates,
/// allowing command routing to reject mentions addressed to another bot without network access per update.
/// </summary>
internal sealed class TelegramBotIdentityStartupService(
    ITelegramClient bot,
    TelegramBotIdentity botIdentity) : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        return botIdentity.EnsureResolvedAsync(bot, cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
