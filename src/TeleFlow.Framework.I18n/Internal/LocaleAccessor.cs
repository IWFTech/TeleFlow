namespace TeleFlow.Telegram.I18n.Internal;

/// <summary>
/// Stores the locale selected by locale middleware for the lifetime of one scoped Telegram update pipeline.
/// </summary>
internal sealed class LocaleAccessor : ILocaleAccessor
{
    private Locale? _current;

    public bool IsAvailable => _current is not null;

    public Locale Current => _current ?? throw new InvalidOperationException(
        "No locale is available. ILocaleAccessor can only be used after locale resolution inside Telegram update processing.");

    public void Initialize(Locale locale)
    {
        ArgumentNullException.ThrowIfNull(locale);

        if (_current is not null)
        {
            throw new InvalidOperationException("The locale has already been resolved for this update scope.");
        }

        _current = locale;
    }
}
