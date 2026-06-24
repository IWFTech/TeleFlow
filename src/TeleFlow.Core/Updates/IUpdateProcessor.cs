namespace TeleFlow.Core.Updates;

public interface IUpdateProcessor
{
    Task ProcessAsync(IUpdatePayload payload, CancellationToken cancellationToken = default);
}
