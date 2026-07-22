using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace TeleFlow.Telegram.I18n;

/// <summary>
/// Represents a validated, normalized locale used by Telegram update resolution and localization engines.
/// The value keeps culture identity explicit without changing ambient process or thread cultures.
/// </summary>
public sealed class Locale : IEquatable<Locale>
{
    public Locale(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmedName = name.Trim();

        if (trimmedName.Contains('_', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Locale names must use BCP 47 '-' separators instead of '_'.",
                nameof(name));
        }

        try
        {
            Culture = CultureInfo.GetCultureInfo(trimmedName);
        }
        catch (CultureNotFoundException exception)
        {
            throw new ArgumentException($"'{name}' is not a valid locale name.", nameof(name), exception);
        }

        if (string.IsNullOrEmpty(Culture.Name))
        {
            throw new ArgumentException("The invariant culture cannot be used as a locale.", nameof(name));
        }

        Name = Culture.Name;
    }

    /// <summary>
    /// Gets the normalized culture name, for example <c>en</c> or <c>ru-RU</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the read-only .NET culture represented by this locale.
    /// </summary>
    public CultureInfo Culture { get; }

    /// <summary>
    /// Attempts to create a locale without throwing for untrusted language codes such as Telegram user metadata.
    /// </summary>
    public static bool TryCreate(string? name, [NotNullWhen(true)] out Locale? locale)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            locale = null;
            return false;
        }

        try
        {
            locale = new Locale(name);
            return true;
        }
        catch (ArgumentException)
        {
            locale = null;
            return false;
        }
    }

    public bool Equals(Locale? other)
    {
        return other is not null &&
               string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return obj is Locale other && Equals(other);
    }

    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
    }

    public override string ToString()
    {
        return Name;
    }
}
