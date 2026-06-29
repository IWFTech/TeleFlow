using Microsoft.Extensions.DependencyInjection;

namespace TeleFlow.Core.Middleware;

internal sealed class UpdateMiddlewareRegistration
{
    public UpdateMiddlewareRegistration(Type middlewareType)
    {
        ArgumentNullException.ThrowIfNull(middlewareType);

        if (!typeof(IUpdateMiddleware).IsAssignableFrom(middlewareType))
        {
            throw new ArgumentException(
                $"Middleware type must implement {nameof(IUpdateMiddleware)}.",
                nameof(middlewareType));
        }

        MiddlewareType = middlewareType;
    }

    public Type MiddlewareType { get; }

    public IUpdateMiddleware Resolve(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return (IUpdateMiddleware)services.GetRequiredService(MiddlewareType);
    }
}
