namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class CallbackDataPrefixAttribute : TeleFlowAttribute
{
    public CallbackDataPrefixAttribute(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Callback data prefix must not be empty.", nameof(prefix));
        }

        Prefix = prefix.Trim();
    }

    public string Prefix { get; }
}
