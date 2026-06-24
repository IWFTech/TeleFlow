namespace TeleFlow.Core.Updates;

public interface IUpdateSource
{
    Task StartAsync(
        Func<IUpdatePayload, CancellationToken, Task> updateHandler,
        CancellationToken cancellationToken = default);
}
