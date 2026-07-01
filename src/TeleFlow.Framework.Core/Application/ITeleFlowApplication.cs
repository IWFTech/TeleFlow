namespace TeleFlow.Framework.Application;

public interface ITeleFlowApplication : IDisposable, IAsyncDisposable
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
