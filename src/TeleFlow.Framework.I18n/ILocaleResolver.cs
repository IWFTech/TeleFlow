namespace TeleFlow.Telegram.I18n;

/// <summary>
/// Resolves an application locale for one Telegram update before handler dispatch.
/// Implementations may use scoped storage services and return <see langword="null"/> when they have no decision.
/// </summary>
public interface ILocaleResolver
{
    /// <summary>
    /// Attempts to resolve a locale for the current Telegram update.
    /// </summary>
    ValueTask<Locale?> TryResolveAsync(
        LocaleResolutionContext context,
        CancellationToken cancellationToken = default);
}
