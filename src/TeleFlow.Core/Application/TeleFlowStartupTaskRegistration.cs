namespace TeleFlow.Core.Application;

/// <summary>
/// Stores a startup task type registered through TeleFlow lifecycle registration APIs.
/// The application lifecycle runner uses this descriptor to create the task from a dedicated
/// dependency injection scope at runtime instead of resolving task instances from the root provider.
/// </summary>
internal sealed class TeleFlowStartupTaskRegistration
{
    public TeleFlowStartupTaskRegistration(Type taskType)
    {
        ArgumentNullException.ThrowIfNull(taskType);

        if (!typeof(ITeleFlowStartupTask).IsAssignableFrom(taskType))
        {
            throw new ArgumentException(
                $"Type '{taskType.FullName}' must implement {nameof(ITeleFlowStartupTask)}.",
                nameof(taskType));
        }

        TaskType = taskType;
    }

    public Type TaskType { get; }
}
