using TeleFlow.Framework.Updates;

namespace TeleFlow.Framework.Middleware;

public interface IUpdateMiddleware
{
    /// <summary>
    /// Invokes this middleware for the current update.
    /// </summary>
    /// <param name="context">The current update context.</param>
    /// <param name="next">The next pipeline step.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "The parameter name intentionally follows established .NET middleware terminology.")]
    Task InvokeAsync(UpdateContext context, UpdateDelegate next);
}
