namespace TeleFlow.Framework.Updates;

public interface IUpdateProcessor
{
    Task ProcessAsync(IUpdatePayload payload, CancellationToken cancellationToken = default);
}
