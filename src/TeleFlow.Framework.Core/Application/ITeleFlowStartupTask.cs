namespace TeleFlow.Framework.Application;

/// <summary>
/// Runs application startup work before the configured update source starts receiving updates.
/// Use startup tasks for DI-composable initialization such as Telegram command setup, cache warmup,
/// or local resource checks that belong to the TeleFlow application lifecycle.
/// </summary>
public interface ITeleFlowStartupTask
{
    /// <summary>
    /// Executes the startup task before update processing begins.
    /// </summary>
    ValueTask ExecuteAsync(CancellationToken cancellationToken = default);
}
