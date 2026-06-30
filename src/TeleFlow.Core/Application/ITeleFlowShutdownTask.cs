namespace TeleFlow.Core.Application;

/// <summary>
/// Runs application shutdown work after the configured update source stops receiving updates.
/// Use shutdown tasks for DI-composable cleanup such as flushing local buffers, metrics, or
/// application-owned resources that should be finalized after update processing ends.
/// </summary>
public interface ITeleFlowShutdownTask
{
    /// <summary>
    /// Executes the shutdown task after update processing stops.
    /// </summary>
    ValueTask ExecuteAsync(CancellationToken cancellationToken = default);
}
