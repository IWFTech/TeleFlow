namespace TeleFlow.Annotations;

/// <summary>
/// Base attribute for project-specific Telegram filters that need per-handler metadata.
/// The framework reads derived attributes during handler registration and passes the attribute instance
/// to the matching typed filter during incoming update selection.
/// </summary>
/// <typeparam name="TFilter">Custom filter type registered in dependency injection.</typeparam>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public abstract class TelegramFilterAttribute<TFilter> : TeleFlowAttribute
    where TFilter : class
{
}
