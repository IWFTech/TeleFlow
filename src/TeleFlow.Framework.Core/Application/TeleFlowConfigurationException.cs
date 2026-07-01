namespace TeleFlow.Framework.Application;

/// <summary>
/// Represents an invalid TeleFlow application setup detected before an update should be processed.
/// The runtime uses this exception for configuration and DI validation failures that users can fix in startup code.
/// </summary>
public sealed class TeleFlowConfigurationException : InvalidOperationException
{
    public TeleFlowConfigurationException(string message)
        : base(message)
    {
    }

    public TeleFlowConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
