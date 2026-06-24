namespace TeleFlow.Core.Application;

public interface ITeleFlowApplication : IDisposable, IAsyncDisposable
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
