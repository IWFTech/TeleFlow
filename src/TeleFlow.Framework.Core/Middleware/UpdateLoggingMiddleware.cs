using Microsoft.Extensions.Logging;
using TeleFlow.Framework.Updates;

namespace TeleFlow.Framework.Middleware;

public sealed class UpdateLoggingMiddleware : IUpdateMiddleware
{
    private static readonly Action<ILogger, Exception?> LogProcessingStarted =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, nameof(LogProcessingStarted)),
            "Started processing update.");

    private static readonly Action<ILogger, Exception?> LogProcessingFinished =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2, nameof(LogProcessingFinished)),
            "Finished processing update.");

    private static readonly Action<ILogger, Exception?> LogProcessingFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(3, nameof(LogProcessingFailed)),
            "Failed processing update.");

    private readonly ILogger<UpdateLoggingMiddleware>? _logger;

    public UpdateLoggingMiddleware()
    {
    }

    public UpdateLoggingMiddleware(ILogger<UpdateLoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(UpdateContext context, UpdateDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (_logger is not null)
        {
            LogProcessingStarted(_logger, null);
        }

        try
        {
            await next(context).ConfigureAwait(false);

            if (_logger is not null)
            {
                LogProcessingFinished(_logger, null);
            }
        }
        catch (Exception exception)
        {
            if (_logger is not null)
            {
                LogProcessingFailed(_logger, exception);
            }

            throw;
        }
    }
}
