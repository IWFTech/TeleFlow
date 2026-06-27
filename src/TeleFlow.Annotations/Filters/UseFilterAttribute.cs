namespace TeleFlow.Annotations;
/// <summary>
/// Attaches a custom Telegram filter to a handler or handler class.
/// </summary>
/// <typeparam name="TFilter">Custom filter type registered in dependency injection.</typeparam>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class UseFilterAttribute<TFilter> : TeleFlowAttribute
    where TFilter : class
{
}
