using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Framework.Application;
using TeleFlow.Framework.Dispatching;
using TeleFlow.Framework.Middleware;

namespace TeleFlow.Framework.Updates;

internal sealed class DefaultUpdateProcessor : IUpdateProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUpdateDispatcher _dispatcher;
    private readonly IReadOnlyList<UpdateMiddlewareRegistration> _middleware;
    private readonly object _validationLock = new();
    private bool _runtimeValidated;

    public DefaultUpdateProcessor(
        IServiceScopeFactory scopeFactory,
        IUpdateDispatcher dispatcher,
        IEnumerable<UpdateMiddlewareRegistration> middleware)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        ArgumentNullException.ThrowIfNull(middleware);

        _middleware = middleware.ToArray();
    }

    public async Task ProcessAsync(IUpdatePayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var scope = _scopeFactory.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            EnsureRuntimeValidated(scope.ServiceProvider);

            var context = new UpdateContext(scope.ServiceProvider, payload, cancellationToken);
            var currentUpdate = scope.ServiceProvider.GetService<IUpdateContextAccessorInitializer>();
            var pipeline = BuildPipeline(scope.ServiceProvider, _dispatcher, _middleware);

            currentUpdate?.Initialize(context);

            try
            {
                await pipeline(context).ConfigureAwait(false);
            }
            finally
            {
                currentUpdate?.Clear(context);
            }
        }
    }

    private void EnsureRuntimeValidated(IServiceProvider services)
    {
        if (_runtimeValidated)
        {
            return;
        }

        lock (_validationLock)
        {
            if (_runtimeValidated)
            {
                return;
            }

            TeleFlowRuntimeValidatorRunner.Validate(services);
            _runtimeValidated = true;
        }
    }

    private static UpdateDelegate BuildPipeline(
        IServiceProvider services,
        IUpdateDispatcher dispatcher,
        IReadOnlyList<UpdateMiddlewareRegistration> middleware)
    {
        UpdateDelegate pipeline = context => dispatcher.DispatchAsync(context, context.CancellationToken);

        for (var index = middleware.Count - 1; index >= 0; index--)
        {
            var current = middleware[index].Resolve(services);
            var next = pipeline;
            pipeline = context => current.InvokeAsync(context, next);
        }

        return pipeline;
    }
}
