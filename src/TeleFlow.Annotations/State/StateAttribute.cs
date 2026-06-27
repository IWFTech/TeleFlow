namespace TeleFlow.Annotations;
/// <summary>
/// Restricts a handler or handler class to updates whose current state matches the specified state id.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class StateAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a state filter.
    /// </summary>
    /// <param name="state">State id that must be active for the handler to match.</param>
    public StateAttribute(string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        State = state;
    }

    /// <summary>
    /// State id that must be active for the handler to match.
    /// </summary>
    public string State { get; }
}
/// <summary>
/// Restricts a handler or handler class to a state from a typed state group.
/// </summary>
/// <typeparam name="TStateGroup">State group type decorated with <see cref="StateGroupAttribute"/>.</typeparam>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class StateAttribute<TStateGroup> : TeleFlowAttribute
{
    /// <summary>
    /// Creates a typed state-group filter.
    /// </summary>
    /// <param name="stateName">State name inside the typed state group.</param>
    public StateAttribute(string stateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        StateName = stateName;
    }

    /// <summary>
    /// State name inside the typed state group.
    /// </summary>
    public string StateName { get; }
}
