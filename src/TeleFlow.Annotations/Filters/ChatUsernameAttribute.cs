namespace TeleFlow.Annotations;
/// <summary>
/// Restricts a handler or handler class to specific Telegram chat usernames.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ChatUsernameAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a chat username filter.
    /// </summary>
    /// <param name="usernames">Telegram chat usernames allowed to match, with or without leading <c>@</c>.</param>
    public ChatUsernameAttribute(params string[] usernames)
    {
        ArgumentNullException.ThrowIfNull(usernames);

        var canonicalUsernames = usernames
            .Select(CanonicalizeUsername)
            .ToArray();

        if (canonicalUsernames.Length == 0)
        {
            throw new ArgumentException("At least one Telegram chat username must be specified.", nameof(usernames));
        }

        if (canonicalUsernames.Any(static username => username.Length == 0))
        {
            throw new ArgumentException("Telegram chat usernames must not be empty.", nameof(usernames));
        }

        Usernames = canonicalUsernames;
    }

    /// <summary>
    /// Canonical Telegram chat usernames allowed to match, without leading <c>@</c>.
    /// </summary>
    public IReadOnlyList<string> Usernames { get; }

    private static string CanonicalizeUsername(string? username)
    {
        if (username is null)
        {
            return string.Empty;
        }

        var value = username.Trim();
        return value.StartsWith('@') ? value[1..] : value;
    }
}
