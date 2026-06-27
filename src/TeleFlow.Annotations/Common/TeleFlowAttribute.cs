namespace TeleFlow.Annotations;
/// <summary>
/// Base type for TeleFlow compile-time metadata attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public abstract class TeleFlowAttribute : Attribute
{
}
