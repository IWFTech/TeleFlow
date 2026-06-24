using System.Diagnostics.CodeAnalysis;

namespace TeleFlow.Annotations;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
[SuppressMessage(
    "Performance",
    "CA1813:Avoid unsealed attributes",
    Justification = "The non-generic catch-all error attribute is intentionally inherited by ErrorAttribute<TException>.")]
public class ErrorAttribute : TeleFlowAttribute
{
    public ErrorAttribute()
    {
    }

    internal ErrorAttribute(Type exceptionType)
    {
        ArgumentNullException.ThrowIfNull(exceptionType);

        ExceptionType = exceptionType;
    }

    public Type? ExceptionType { get; }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ErrorAttribute<TException> : ErrorAttribute
    where TException : Exception
{
    public ErrorAttribute()
        : base(typeof(TException))
    {
    }
}
