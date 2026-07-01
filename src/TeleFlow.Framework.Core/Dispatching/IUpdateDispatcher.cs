using TeleFlow.Framework.Updates;

namespace TeleFlow.Framework.Dispatching;

public interface IUpdateDispatcher
{
    Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default);
}
