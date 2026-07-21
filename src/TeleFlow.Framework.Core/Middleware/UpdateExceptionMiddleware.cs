using Microsoft.Extensions.Logging;
using TeleFlow.Framework.Updates;

namespace TeleFlow.Framework.Middleware;

public sealed class UpdateExceptionMiddleware : IUpdateMiddleware
{
    private static readonly Action<ILogger, Exception?> LogUnhandledException =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1, nameof(LogUnhandledException)),
            "Unhandled exception while processing update.");

    private readonly ILogger<UpdateExceptionMiddleware>? _logger;

    public UpdateExceptionMiddleware()
    {
    }

    public UpdateExceptionMiddleware(ILogger<UpdateExceptionMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(UpdateContext context, UpdateDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (Exception exception) when (!IsUpdateCancellation(exception, context.CancellationToken))
        {
            if (_logger is not null)
            {
                LogUnhandledException(_logger, exception);
            }

            throw;
        }
    }

    private static bool IsUpdateCancellation(Exception exception, CancellationToken cancellationToken)
    {
        return exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
    }
}
