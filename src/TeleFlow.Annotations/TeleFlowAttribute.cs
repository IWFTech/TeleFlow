namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public abstract class TeleFlowAttribute : Attribute
{
}
