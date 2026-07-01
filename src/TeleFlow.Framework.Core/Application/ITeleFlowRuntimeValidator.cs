namespace TeleFlow.Framework.Application;

/// <summary>
/// Validates framework runtime configuration against the final service provider before updates are processed.
/// Framework layers register validators for metadata that cannot be checked by the DI container alone.
/// </summary>
public interface ITeleFlowRuntimeValidator
{
    /// <summary>
    /// Validates the final runtime service provider and throws <see cref="TeleFlowConfigurationException"/>
    /// when the application setup is invalid.
    /// </summary>
    void Validate(IServiceProvider services);
}
