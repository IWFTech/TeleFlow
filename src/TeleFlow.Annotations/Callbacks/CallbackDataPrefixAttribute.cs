namespace TeleFlow.Annotations;
/// <summary>
/// Restricts callback query routing to callback data with the specified prefix.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class CallbackDataPrefixAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a callback data prefix filter.
    /// </summary>
    /// <param name="prefix">Callback data prefix to match.</param>
    public CallbackDataPrefixAttribute(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Callback data prefix must not be empty.", nameof(prefix));
        }

        Prefix = prefix.Trim();
    }

    /// <summary>
    /// Callback data prefix that must be present on the incoming callback query.
    /// </summary>
    public string Prefix { get; }
}
