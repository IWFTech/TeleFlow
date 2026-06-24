namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class StateAttribute : TeleFlowAttribute
{
    public StateAttribute(string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        State = state;
    }

    public string State { get; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class StateAttribute<TStateGroup> : TeleFlowAttribute
{
    public StateAttribute(string stateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        StateName = stateName;
    }

    public string StateName { get; }
}
