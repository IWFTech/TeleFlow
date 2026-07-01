namespace TeleFlow.Telegram;

/// <summary>
/// Defines a custom Telegram handler filter that receives only the current update context.
/// Use this contract for global yes/no checks that do not need attribute metadata.
/// </summary>
/// <typeparam name="TContext">Telegram update context type the filter can evaluate.</typeparam>
public interface ITelegramFilter<in TContext>
    where TContext : TelegramUpdateContext
{
    /// <summary>
    /// Returns whether the current update should continue to the handler guarded by this filter.
    /// </summary>
    /// <param name="context">Current Telegram update context.</param>
    /// <param name="cancellationToken">Cancellation token for the current update pipeline.</param>
    /// <returns><see langword="true"/> when the handler remains eligible; otherwise <see langword="false"/>.</returns>
    ValueTask<bool> MatchesAsync(
        TContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a custom Telegram handler filter that receives metadata from a derived
/// <see cref="TeleFlow.Annotations.TelegramFilterAttribute{TFilter}"/> instance.
/// Use this contract for reusable filters such as per-handler permissions, feature flags, or cooldown keys.
/// </summary>
/// <typeparam name="TContext">Telegram update context type the filter can evaluate.</typeparam>
/// <typeparam name="TAttribute">Custom filter attribute type that carries handler metadata.</typeparam>
public interface ITelegramFilter<in TContext, in TAttribute>
    where TContext : TelegramUpdateContext
    where TAttribute : Attribute
{
    /// <summary>
    /// Returns whether the current update should continue to the handler guarded by this filter.
    /// </summary>
    /// <param name="context">Current Telegram update context.</param>
    /// <param name="attribute">Custom filter attribute instance attached to the handler or handler class.</param>
    /// <param name="cancellationToken">Cancellation token for the current update pipeline.</param>
    /// <returns><see langword="true"/> when the handler remains eligible; otherwise <see langword="false"/>.</returns>
    ValueTask<bool> MatchesAsync(
        TContext context,
        TAttribute attribute,
        CancellationToken cancellationToken = default);
}
