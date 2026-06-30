using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using TeleFlow.Core.Updates;

namespace TeleFlow.Core.Application;

public sealed class TeleFlowApplication : ITeleFlowApplication
{
    private readonly IServiceProvider _services;
    private readonly IUpdateSource _updateSource;
    private readonly IUpdateProcessor _updateProcessor;
    private readonly TeleFlowApplicationLifecycleRunner _lifecycle;
    private bool _disposed;

    internal TeleFlowApplication(
        IServiceProvider services,
        IUpdateSource updateSource,
        IUpdateProcessor updateProcessor,
        TeleFlowApplicationLifecycleRunner lifecycle)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _updateSource = updateSource ?? throw new ArgumentNullException(nameof(updateSource));
        _updateProcessor = updateProcessor ?? throw new ArgumentNullException(nameof(updateProcessor));
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
    }

    public static ITeleFlowApplicationBuilder CreateBuilder(string[]? args = null)
    {
        return new TeleFlowApplicationBuilder(args);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The application must run shutdown tasks for any update-source failure while preserving and rethrowing the original exception.")]
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lifecycle.RunStartupAsync(cancellationToken).ConfigureAwait(false);

        ExceptionDispatchInfo? updateSourceException = null;

        try
        {
            await _updateSource.StartAsync(ProcessUpdateAsync, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            updateSourceException = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            await _lifecycle.RunShutdownAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception shutdownException) when (updateSourceException is not null)
        {
            throw new AggregateException(updateSourceException.SourceException, shutdownException);
        }

        if (updateSourceException is not null)
        {
            updateSourceException.Throw();
        }
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
