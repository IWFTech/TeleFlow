namespace TeleFlow.Framework.Updates;

/// <summary>
/// Provides scoped read-only access to the update currently being processed by the TeleFlow runtime pipeline.
/// Application services can use this accessor when they need update identity without accepting framework
/// handler contexts directly.
/// </summary>
public interface IUpdateContextAccessor
{
    /// <summary>
    /// Gets whether a current update is available in the active DI scope.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets the current update context or throws when called outside update processing.
    /// </summary>
    UpdateContext Current { get; }

    /// <summary>
    /// Attempts to get the current update context without throwing outside update processing.
    /// </summary>
    bool TryGetCurrent(out UpdateContext context);
}
