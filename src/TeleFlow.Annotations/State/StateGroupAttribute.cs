namespace TeleFlow.Annotations;
/// <summary>
/// Defines a stable prefix for a typed group of conversation states.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class StateGroupAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates typed state group metadata.
    /// </summary>
    /// <param name="prefix">Prefix used when composing state ids for the group.</param>
    public StateGroupAttribute(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        Prefix = prefix;
    }

    /// <summary>
    /// Prefix used when composing state ids for the group.
    /// </summary>
    public string Prefix { get; }
}
