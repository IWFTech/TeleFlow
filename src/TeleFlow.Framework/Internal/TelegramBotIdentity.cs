using TeleFlow.Telegram.Internal;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Holds the normalized identity of the current Telegram bot for framework features that must distinguish
/// commands addressed to this bot from commands addressed to another bot. Identity is configured locally or
/// resolved once during transport startup; handler selection only reads the cached value.
/// </summary>
internal sealed class TelegramBotIdentity
{
    private readonly object _sync = new();
    private string? _username;
    private Task? _resolutionTask;

    public TelegramBotIdentity(TelegramBotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!TelegramBotUsernameNormalizer.TryNormalize(options.BotUsername, out _username, out var error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public bool MatchesMention(ReadOnlySpan<char> mention)
    {
        var username = Volatile.Read(ref _username);

        return username is not null &&
               username.AsSpan().Equals(mention, StringComparison.OrdinalIgnoreCase);
    }

    public Task EnsureResolvedAsync(
        ITelegramClient bot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bot);

        if (Volatile.Read(ref _username) is not null)
        {
            return Task.CompletedTask;
        }

        lock (_sync)
        {
            if (_username is not null)
            {
                return Task.CompletedTask;
            }

            return _resolutionTask ??= ResolveAsync(bot, cancellationToken);
        }
    }

    private async Task ResolveAsync(ITelegramClient bot, CancellationToken cancellationToken)
    {
        try
        {
            var user = await bot.GetMeAsync(cancellationToken).ConfigureAwait(false);

            if (!TelegramBotUsernameNormalizer.TryNormalize(user.Username, out var username, out var error) ||
                username is null)
            {
                throw new InvalidOperationException(
                    $"Telegram bot identity could not be resolved because getMe returned an invalid username. {error}");
            }

            Volatile.Write(ref _username, username);
        }
        catch
        {
            lock (_sync)
            {
                _resolutionTask = null;
            }

            throw;
        }
    }
}
