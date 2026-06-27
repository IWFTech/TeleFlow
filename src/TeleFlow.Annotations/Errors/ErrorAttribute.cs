using System.Diagnostics.CodeAnalysis;

namespace TeleFlow.Annotations;

/// <summary>
/// Marks a method as a Telegram handler error handler.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
[SuppressMessage(
    "Performance",
    "CA1813:Avoid unsealed attributes",
    Justification = "The non-generic catch-all error attribute is intentionally inherited by ErrorAttribute<TException>.")]
public class ErrorAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a catch-all handler error attribute.
    /// </summary>
    public ErrorAttribute()
    {
    }

    internal ErrorAttribute(Type exceptionType)
    {
        ArgumentNullException.ThrowIfNull(exceptionType);

        ExceptionType = exceptionType;
    }

    /// <summary>
    /// Exception type handled by the method, or <see langword="null"/> for catch-all error handlers.
    /// </summary>
    public Type? ExceptionType { get; }
}
/// <summary>
/// Marks a method as handling a specific exception type raised by a Telegram handler.
/// </summary>
/// <typeparam name="TException">Exception type handled by the method.</typeparam>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ErrorAttribute<TException> : ErrorAttribute
    where TException : Exception
{
    /// <summary>
    /// Creates a typed handler error attribute.
    /// </summary>
    public ErrorAttribute()
        : base(typeof(TException))
    {
    }
}
