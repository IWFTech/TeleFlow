namespace TeleFlow.Annotations;
/// <summary>
/// Marks a handler method as a named step inside its enclosing scene.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class SceneStepAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates scene step metadata.
    /// </summary>
    /// <param name="stateName">State name inside the enclosing scene prefix.</param>
    public SceneStepAttribute(string stateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        StateName = stateName;
    }

    /// <summary>
    /// State name inside the enclosing scene prefix.
    /// </summary>
    public string StateName { get; }
}
