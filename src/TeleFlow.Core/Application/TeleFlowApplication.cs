using TeleFlow.Core.Updates;

namespace TeleFlow.Core.Application;

public sealed class TeleFlowApplication : ITeleFlowApplication
{
    private readonly IServiceProvider _services;
    private readonly IUpdateSource _updateSource;
    private readonly IUpdateProcessor _updateProcessor;
    private bool _disposed;

    internal TeleFlowApplication(
        IServiceProvider services,
        IUpdateSource updateSource,
        IUpdateProcessor updateProcessor)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _updateSource = updateSource ?? throw new ArgumentNullException(nameof(updateSource));
        _updateProcessor = updateProcessor ?? throw new ArgumentNullException(nameof(updateProcessor));
    }

    public static ITeleFlowApplicationBuilder CreateBuilder(string[]? args = null)
    {
        return new TeleFlowApplicationBuilder(args);
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _updateSource.StartAsync(ProcessUpdateAsync, cancellationToken);
    }

    internal Task ProcessUpdateAsync(IUpdatePayload payload, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(payload);

        return _updateProcessor.ProcessAsync(payload, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_services is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_services is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            return;
        }

        if (_services is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

}
