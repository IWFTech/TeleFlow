using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Core.States;
using TeleFlow.Core.Updates;

namespace TeleFlow.Core.Middleware;

public sealed class UpdateStateMiddleware : IUpdateMiddleware
{
    private readonly IStateKeyFactory _stateKeyFactory;

    public UpdateStateMiddleware(IStateKeyFactory stateKeyFactory)
    {
        ArgumentNullException.ThrowIfNull(stateKeyFactory);
        _stateKeyFactory = stateKeyFactory;
    }

    public async Task InvokeAsync(UpdateContext context, UpdateDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (_stateKeyFactory.TryCreateStateKey(context, out var key))
        {
            var stateStore = context.Services.GetRequiredService<IStateStore>();
            var dataStore = context.Services.GetService<IStateDataStore>();
            var dataSerializer = context.Services.GetService<IStateDataSerializer>();
            var historyStore = context.Services.GetService<IStateHistoryStore>();

            context.Items[UpdateStateContextKeys.State] = new UpdateState(
                stateStore,
                key,
                dataStore,
                dataSerializer,
                historyStore);
        }

        await next(context).ConfigureAwait(false);
    }
}
