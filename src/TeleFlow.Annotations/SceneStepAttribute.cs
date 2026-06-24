namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class SceneStepAttribute : TeleFlowAttribute
{
    public SceneStepAttribute(string stateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        StateName = stateName;
    }

    public string StateName { get; }
}
