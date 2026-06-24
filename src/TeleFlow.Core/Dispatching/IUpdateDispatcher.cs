using TeleFlow.Core.Updates;

namespace TeleFlow.Core.Dispatching;

public interface IUpdateDispatcher
{
    Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default);
}
