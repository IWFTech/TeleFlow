namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TelegramModuleAttribute : TeleFlowAttribute
{
    public TelegramModuleAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public string Name { get; }
}
