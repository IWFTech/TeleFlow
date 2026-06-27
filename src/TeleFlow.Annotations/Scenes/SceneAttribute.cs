namespace TeleFlow.Annotations;
/// <summary>
/// Defines a scene prefix for a handler class that owns multiple scene steps.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SceneAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates scene metadata for a handler class.
    /// </summary>
    /// <param name="prefix">Prefix used when composing scene state ids.</param>
    public SceneAttribute(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        Prefix = prefix;
    }

    /// <summary>
    /// Prefix used when composing scene state ids.
    /// </summary>
    public string Prefix { get; }
}
