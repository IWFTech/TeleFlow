namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SceneAttribute : TeleFlowAttribute
{
    public SceneAttribute(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        Prefix = prefix;
    }

    public string Prefix { get; }
}
