namespace TeleFlow.Telegram;

public interface ITelegramFilter<in TContext>
    where TContext : TelegramUpdateContext
{
    ValueTask<bool> MatchesAsync(
        TContext context,
        CancellationToken cancellationToken = default);
}
