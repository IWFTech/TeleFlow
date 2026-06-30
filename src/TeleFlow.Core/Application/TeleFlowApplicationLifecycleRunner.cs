using Microsoft.Extensions.DependencyInjection;

namespace TeleFlow.Core.Application;

/// <summary>
/// Executes TeleFlow application lifecycle tasks around update source execution.
/// This runner is transport-agnostic so plain application execution and future hosting integration
/// can share the same startup and shutdown semantics.
/// </summary>
internal sealed class TeleFlowApplicationLifecycleRunner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyList<TeleFlowStartupTaskRegistration> _startupTasks;
    private readonly IReadOnlyList<TeleFlowShutdownTaskRegistration> _shutdownTasks;

    public TeleFlowApplicationLifecycleRunner(
        IServiceScopeFactory scopeFactory,
        IReadOnlyList<TeleFlowStartupTaskRegistration> startupTasks,
        IReadOnlyList<TeleFlowShutdownTaskRegistration> shutdownTasks)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _startupTasks = startupTasks ?? throw new ArgumentNullException(nameof(startupTasks));
        _shutdownTasks = shutdownTasks ?? throw new ArgumentNullException(nameof(shutdownTasks));
    }

    public async ValueTask RunStartupAsync(CancellationToken cancellationToken = default)
    {
        foreach (var registration in _startupTasks)
        {
            await ExecuteStartupTaskAsync(registration.TaskType, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask RunShutdownAsync(CancellationToken cancellationToken = default)
    {
        for (var index = _shutdownTasks.Count - 1; index >= 0; index--)
        {
            await ExecuteShutdownTaskAsync(_shutdownTasks[index].TaskType, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask ExecuteStartupTaskAsync(
        Type taskType,
        CancellationToken cancellationToken)
    {
        var scope = _scopeFactory.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var task = (ITeleFlowStartupTask)ActivatorUtilities.CreateInstance(scope.ServiceProvider, taskType);

            await task.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask ExecuteShutdownTaskAsync(
        Type taskType,
        CancellationToken cancellationToken)
    {
        var scope = _scopeFactory.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var task = (ITeleFlowShutdownTask)ActivatorUtilities.CreateInstance(scope.ServiceProvider, taskType);

            await task.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
