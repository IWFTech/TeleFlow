namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class UseFilterAttribute<TFilter> : TeleFlowAttribute
    where TFilter : class
{
}
