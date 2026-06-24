using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Core.Dispatching;
using TeleFlow.Core.Middleware;

namespace TeleFlow.Core.Updates;

internal sealed class DefaultUpdateProcessor : IUpdateProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UpdateDelegate _pipeline;

    public DefaultUpdateProcessor(
        IServiceScopeFactory scopeFactory,
        IUpdateDispatcher dispatcher,
        IEnumerable<IUpdateMiddleware> middleware)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(middleware);

        _pipeline = BuildPipeline(dispatcher, middleware.ToArray());
    }

    public async Task ProcessAsync(IUpdatePayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        using var scope = _scopeFactory.CreateScope();
        var context = new UpdateContext(scope.ServiceProvider, payload, cancellationToken);

        await _pipeline(context).ConfigureAwait(false);
    }

    private static UpdateDelegate BuildPipeline(
        IUpdateDispatcher dispatcher,
        IUpdateMiddleware[] middleware)
    {
        UpdateDelegate pipeline = context => dispatcher.DispatchAsync(context, context.CancellationToken);

        for (var index = middleware.Length - 1; index >= 0; index--)
        {
            var current = middleware[index];
            var next = pipeline;
            pipeline = context => current.InvokeAsync(context, next);
        }

        return pipeline;
    }
}
