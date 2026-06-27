namespace TeleFlow.Annotations;
/// <summary>
/// Defines the compact callback data prefix for a typed callback payload.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class CallbackDataAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates callback data metadata for the payload type.
    /// </summary>
    /// <param name="prefix">Stable callback data prefix used to identify the payload type.</param>
    public CallbackDataAttribute(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        Prefix = prefix;
    }

    /// <summary>
    /// Stable prefix used when serializing and matching the callback payload.
    /// </summary>
    public string Prefix { get; }
}
