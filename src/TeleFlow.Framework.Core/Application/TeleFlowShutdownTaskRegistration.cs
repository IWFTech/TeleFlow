namespace TeleFlow.Framework.Application;

/// <summary>
/// Stores a shutdown task type registered through TeleFlow lifecycle registration APIs.
/// The application lifecycle runner consumes these descriptors in reverse registration order
/// after the update source has stopped.
/// </summary>
internal sealed class TeleFlowShutdownTaskRegistration
{
    public TeleFlowShutdownTaskRegistration(Type taskType)
    {
        ArgumentNullException.ThrowIfNull(taskType);

        if (!typeof(ITeleFlowShutdownTask).IsAssignableFrom(taskType))
        {
            throw new ArgumentException(
                $"Type '{taskType.FullName}' must implement {nameof(ITeleFlowShutdownTask)}.",
                nameof(taskType));
        }

        TaskType = taskType;
    }

    public Type TaskType { get; }
}
