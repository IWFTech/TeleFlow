namespace TeleFlow.Core.Updates;

public class UpdateContext
{
    private Dictionary<object, object?>? _items;

    public UpdateContext(
        IServiceProvider services,
        IUpdatePayload payload,
        CancellationToken cancellationToken = default)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        CancellationToken = cancellationToken;
    }

    public IServiceProvider Services { get; }

    public IUpdatePayload Payload { get; }

    public CancellationToken CancellationToken { get; }

    public IDictionary<object, object?> Items => _items ??= new Dictionary<object, object?>();
}
