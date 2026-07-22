namespace TeleFlow.Telegram.I18n.Fluent;

/// <summary>
/// Represents one named value supplied to a Fluent message without dictionaries or anonymous-object reflection.
/// </summary>
public readonly record struct I18nArgument
{
    public I18nArgument(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!IsValidIdentifier(name))
        {
            throw new ArgumentException(
                "Fluent argument names must start with an ASCII letter and contain only ASCII letters, digits, '-' or '_'.",
                nameof(name));
        }

        Name = name;
        Value = value;
    }

    /// <summary>
    /// Gets the Fluent variable name without the leading <c>$</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the application value converted by the Fluent adapter at formatting time.
    /// </summary>
    public object? Value { get; }

    public static implicit operator I18nArgument((string Name, object? Value) argument)
    {
        return new I18nArgument(argument.Name, argument.Value);
    }

    private static bool IsValidIdentifier(string name)
    {
        if (!char.IsAsciiLetter(name[0]))
        {
            return false;
        }

        for (var index = 1; index < name.Length; index++)
        {
            var character = name[index];

            if (!char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_')
            {
                return false;
            }
        }

        return true;
    }
}
