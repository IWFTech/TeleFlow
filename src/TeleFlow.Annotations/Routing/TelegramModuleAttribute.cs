namespace TeleFlow.Annotations;
/// <summary>
/// Assigns a stable module name to a Telegram handler class.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TelegramModuleAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates module metadata for a handler class.
    /// </summary>
    /// <param name="name">Stable module name used in handler metadata and diagnostics.</param>
    public TelegramModuleAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>
    /// Stable module name used in handler metadata and diagnostics.
    /// </summary>
    public string Name { get; }
}
