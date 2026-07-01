namespace TeleFlow.Framework.States;

/// <summary>
/// Identifies the owner and isolation boundary of state records used by state stores, state data,
/// wizard history, and future storage-backed coordination primitives.
/// </summary>
public readonly record struct StateKey(
    string Namespace,
    string Scope,
    string Subject,
    string? Partition,
    string Destiny)
{
    private readonly string _namespace = ValidateRequired(Namespace, nameof(Namespace));
    private readonly string _scope = ValidateRequired(Scope, nameof(Scope));
    private readonly string _subject = ValidateRequired(Subject, nameof(Subject));
    private readonly string? _partition = ValidateOptional(Partition, nameof(Partition));
    private readonly string _destiny = ValidateRequired(Destiny, nameof(Destiny));

    public StateKey(string scope, string subject)
        : this(
            StateKeyDefaults.DefaultNamespace,
            scope,
            subject,
            null,
            StateKeyDefaults.DefaultDestiny)
    {
    }

    public StateKey(string scope, string subject, string? partition)
        : this(
            StateKeyDefaults.DefaultNamespace,
            scope,
            subject,
            partition,
            StateKeyDefaults.DefaultDestiny)
    {
    }

    public string Namespace
    {
        get => _namespace;
        init => _namespace = ValidateRequired(value, nameof(Namespace));
    }

    public string Scope
    {
        get => _scope;
        init => _scope = ValidateRequired(value, nameof(Scope));
    }

    public string Subject
    {
        get => _subject;
        init => _subject = ValidateRequired(value, nameof(Subject));
    }

    public string? Partition
    {
        get => _partition;
        init => _partition = ValidateOptional(value, nameof(Partition));
    }

    public string Destiny
    {
        get => _destiny;
        init => _destiny = ValidateRequired(value, nameof(Destiny));
    }

    public static StateKey Create(string scope, string subject, string? partition = null)
    {
        return new StateKey(scope, subject, partition);
    }

    public static StateKey Create(
        string keyNamespace,
        string scope,
        string subject,
        string? partition,
        string destiny)
    {
        return new StateKey(keyNamespace, scope, subject, partition, destiny);
    }

    private static string ValidateRequired(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} must be provided.", parameterName);
        }

        return value;
    }

    private static string? ValidateOptional(string? value, string parameterName)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} must be null or non-empty.", parameterName);
        }

        return value;
    }
}
