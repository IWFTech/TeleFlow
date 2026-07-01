namespace TeleFlow.Framework.Updates;

/// <summary>
/// Stores the current update context for one DI update scope so middleware, dispatchers, and scoped
/// application services observe the same update identity during processing.
/// </summary>
internal sealed class DefaultUpdateContextAccessor : IUpdateContextAccessor, IUpdateContextAccessorInitializer
{
    private UpdateContext? _current;

    public bool IsAvailable => _current is not null;

    public UpdateContext Current => _current ?? throw new InvalidOperationException(
        "No current update is available. IUpdateContextAccessor can only be used inside TeleFlow update processing, not outside an update scope.");

    public bool TryGetCurrent(out UpdateContext context)
    {
        if (_current is null)
        {
            context = null!;
            return false;
        }

        context = _current;
        return true;
    }

    public void Initialize(UpdateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_current is not null)
        {
            throw new InvalidOperationException("The current update accessor has already been initialized for this update scope.");
        }

        _current = context;
    }

    public void Clear(UpdateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (ReferenceEquals(_current, context))
        {
            _current = null;
        }
    }
}
