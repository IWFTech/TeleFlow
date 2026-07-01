namespace TeleFlow.Framework.States;

public readonly record struct StateKey(string Scope, string Subject, string? Partition = null)
{
    public static StateKey Create(string scope, string subject, string? partition = null)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("Scope must be provided.", nameof(scope));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Subject must be provided.", nameof(subject));
        }

        return new StateKey(scope, subject, partition);
    }
}
