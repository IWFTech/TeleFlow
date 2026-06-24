namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ChatUsernameAttribute : TeleFlowAttribute
{
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
