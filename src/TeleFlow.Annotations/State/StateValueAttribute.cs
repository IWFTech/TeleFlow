namespace TeleFlow.Annotations;
/// <summary>
/// Overrides the generated state value for a property inside a typed state group.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class StateValueAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates state value metadata for a state group property.
    /// </summary>
    /// <param name="value">State value to use instead of the property name.</param>
    public StateValueAttribute(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>
    /// State value used instead of the property name.
    /// </summary>
    public string Value { get; }
}
