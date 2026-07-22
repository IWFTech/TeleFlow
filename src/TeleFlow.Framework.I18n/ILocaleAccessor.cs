namespace TeleFlow.Telegram.I18n;

/// <summary>
/// Provides scoped synchronous access to the locale resolved once for the current Telegram update.
/// Application handlers and middleware use it after the locale middleware has completed resolution.
/// </summary>
public interface ILocaleAccessor
{
    /// <summary>
    /// Gets whether a locale has been resolved in the current update scope.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets the current update locale or throws when used outside the resolved update pipeline.
    /// </summary>
    Locale Current { get; }
}
