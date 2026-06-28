using Microsoft.Extensions.Logging;
using TeleFlow.Core.Dispatching;
using TeleFlow.Core.States;
using TeleFlow.Core.Updates;
using TeleFlow.Telegram.Internal;

namespace TeleFlow.Telegram.Internal.Handlers;

/// <summary>
/// Selects and invokes the Telegram handler for a single update payload.
/// </summary>
internal sealed partial class TelegramHandlerDispatcher : IUpdateDispatcher
{
    private readonly TelegramHandlerSelector _selector;
    private readonly TelegramErrorHandlerIndex _errorHandlerIndex;
    private readonly TimeProvider _timeProvider;
    private readonly TelegramAutoAnswerCallbackDescriptor? _globalAutoAnswerCallback;
    private readonly ILogger<TelegramHandlerDispatcher> _logger;

    /// <summary>
    /// Creates a dispatcher from registered handler descriptors and framework services.
    /// </summary>
    public TelegramHandlerDispatcher(
        IEnumerable<TelegramHandlerDescriptor> descriptors,
        IEnumerable<TelegramErrorHandlerDescriptor> errorHandlers,
        IEnumerable<TelegramAutoAnswerCallbackOptions> autoAnswerCallbackOptions,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(errorHandlers);
        ArgumentNullException.ThrowIfNull(autoAnswerCallbackOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var table = new TelegramHandlerTable(descriptors);

        _selector = new TelegramHandlerSelector(table);
        _errorHandlerIndex = new TelegramErrorHandlerIndex(errorHandlers);
        _timeProvider = timeProvider;
        _globalAutoAnswerCallback = CreateGlobalAutoAnswerDescriptor(autoAnswerCallbackOptions.LastOrDefault());
        _logger = loggerFactory.CreateLogger<TelegramHandlerDispatcher>();
    }

    /// <summary>
    /// Dispatches one update through handler selection, invocation, error handlers, and callback auto-answering.
    /// </summary>
    public async Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Payload is not TelegramUpdatePayload payload)
        {
            return;
        }

        var debugEnabled = _logger.IsEnabled(LogLevel.Debug);
        string? updateType = null;
        string GetUpdateType() => updateType ??= TelegramUpdateLogFormatter.GetUpdateType(payload.Update);

        var effectiveCancellationToken = cancellationToken.CanBeCanceled
            ? cancellationToken
            : context.CancellationToken;
        var currentState = _selector.HasStatefulHandlers
            ? await GetCurrentStateAsync(context, effectiveCancellationToken).ConfigureAwait(false)
            : null;
        var matchStarted = debugEnabled ? _timeProvider.GetTimestamp() : 0;
        TelegramRouteSelection? selection = null;
        TelegramUpdateContext? telegramContext = null;

        if (payload.Update.Message is not null)
        {
            var messageContext = context.GetMessageContext();
            selection = await _selector.SelectMessageHandlerAsync(
                messageContext,
                currentState,
                effectiveCancellationToken).ConfigureAwait(false);
            telegramContext = messageContext;
        }

        if (selection is null && payload.Update.CallbackQuery is not null)
        {
            var callbackContext = context.GetCallbackQueryContext();
            selection = await _selector.SelectCallbackHandlerAsync(
                callbackContext,
                currentState,
                effectiveCancellationToken).ConfigureAwait(false);
            telegramContext = callbackContext;
        }

        if (selection is null && (payload.Update.ChatMember is not null || payload.Update.MyChatMember is not null))
        {
            var chatMemberContext = context.GetChatMemberUpdatedContext();
            var routeKind = payload.Update.ChatMember is not null
                ? TelegramRouteKind.ChatMemberUpdated
                : TelegramRouteKind.MyChatMemberUpdated;
            selection = await _selector.SelectChatMemberHandlerAsync(
                chatMemberContext,
                routeKind,
                currentState,
                effectiveCancellationToken).ConfigureAwait(false);
            telegramContext = chatMemberContext;
        }

        var matchElapsedMilliseconds = debugEnabled ? GetElapsedMilliseconds(matchStarted) : 0;

        if (selection is null || telegramContext is null)
        {
            if (debugEnabled)
            {
                LogNoHandlerMatched(
                    _logger,
                    payload.Update.UpdateId,
                    GetUpdateType(),
                    matchElapsedMilliseconds);
            }

            return;
        }

        string? handlerName = null;
        string? routeName = null;
        string GetHandlerName() => handlerName ??= TelegramUpdateLogFormatter.FormatHandler(selection.Handler);
        string GetRouteName() => routeName ??= TelegramUpdateLogFormatter.FormatRoute(selection.Route);

        if (debugEnabled)
        {
            LogHandlerMatched(
                _logger,
                payload.Update.UpdateId,
                GetUpdateType(),
                GetHandlerName(),
                GetRouteName(),
                selection.Handler.ModuleName ?? string.Empty,
                selection.Handler.SceneName ?? string.Empty,
                matchElapsedMilliseconds);
        }

        var handlerTimingEnabled = debugEnabled;
        var handlerStarted = handlerTimingEnabled ? _timeProvider.GetTimestamp() : 0;
        using var requestTimingScope = handlerTimingEnabled
            ? TelegramHandlerRequestTimingScope.Begin()
            : null;

        try
        {
            await TelegramHandlerInvoker.InvokeAsync(
                selection,
                telegramContext,
                effectiveCancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!IsUpdateCancellation(exception, effectiveCancellationToken))
        {
            HandlerFailureLogContext? logContext = null;
            HandlerFailureLogContext GetLogContext()
            {
                logContext ??= new HandlerFailureLogContext(
                    payload.Update.UpdateId,
                    GetUpdateType(),
                    GetHandlerName(),
                    GetRouteName(),
                    selection.Handler.ModuleName ?? string.Empty,
                    selection.Handler.SceneName ?? string.Empty,
                    exception.GetType().FullName ?? exception.GetType().Name);

                return logContext.Value;
            }

            if (_logger.IsEnabled(LogLevel.Error))
            {
                LogHandlerFailure(
                    exception,
                    GetLogContext(),
                    handlerTimingEnabled,
                    handlerStarted,
                    requestTimingScope);
            }

            if (_errorHandlerIndex.HasHandlers &&
                await TryHandleErrorAsync(
                    selection,
                    telegramContext,
                    exception,
                    debugEnabled ? GetLogContext() : null,
                    effectiveCancellationToken).ConfigureAwait(false))
            {
                return;
            }

            throw;
        }

        await AutoAnswerCallbackAsync(
            selection.Handler,
            telegramContext,
            effectiveCancellationToken).ConfigureAwait(false);

        if (!handlerTimingEnabled)
        {
            return;
        }

        var completedHandlerElapsed = _timeProvider.GetElapsedTime(handlerStarted);
        var completedTiming = requestTimingScope!.CreateSummary(_timeProvider, completedHandlerElapsed);

        LogHandlerCompleted(
            _logger,
            payload.Update.UpdateId,
            GetUpdateType(),
            GetHandlerName(),
            GetRouteName(),
            completedHandlerElapsed.TotalMilliseconds,
            completedTiming.RequestCount,
            completedTiming.RequestElapsedMilliseconds,
            completedTiming.HandlerLogicElapsedMilliseconds);
    }

    /// <summary>
    /// Reads current state only when the selector contains stateful handlers.
    /// </summary>
    private static async ValueTask<string?> GetCurrentStateAsync(
        UpdateContext context,
        CancellationToken cancellationToken)
    {
        return context.TryGetState(out var state)
            ? await state.GetAsync(cancellationToken).ConfigureAwait(false)
            : null;
    }

    /// <summary>
    /// Returns elapsed milliseconds from a timestamp captured by the dispatcher time provider.
    /// </summary>
    private double GetElapsedMilliseconds(long startingTimestamp)
    {
        return _timeProvider.GetElapsedTime(startingTimestamp).TotalMilliseconds;
    }

    /// <summary>
    /// Sends the configured callback answer after a callback handler when the handler did not answer it explicitly.
    /// </summary>
    private async Task AutoAnswerCallbackAsync(
        TelegramHandlerDescriptor handler,
        TelegramUpdateContext context,
        CancellationToken cancellationToken)
    {
        if (context is not CallbackQueryContext callbackContext ||
            callbackContext.IsCallbackQueryAnswered)
        {
            return;
        }

        var autoAnswer = handler.AutoAnswerCallback ?? _globalAutoAnswerCallback;

        if (autoAnswer is not { Enabled: true })
        {
            return;
        }

        await callbackContext.Callback.AnswerAsync(
            autoAnswer.Text,
            autoAnswer.ShowAlert ? true : null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tries registered Telegram error handlers in deterministic priority order.
    /// </summary>
    private async ValueTask<bool> TryHandleErrorAsync(
        TelegramRouteSelection selection,
        TelegramUpdateContext telegramContext,
        Exception exception,
        HandlerFailureLogContext? logContext,
        CancellationToken cancellationToken)
    {
        var errorContext = new TelegramErrorContext(
            exception,
            telegramContext,
            selection.Handler.HandlerType,
            selection.Handler.MethodName,
            selection.Handler.ModuleName,
            selection.Handler.SceneName,
            selection.RouteValues);

        foreach (var errorHandler in _errorHandlerIndex.GetCandidates(selection, telegramContext, exception))
        {
            var result = await TelegramErrorHandlerInvoker.InvokeAsync(
                errorHandler,
                errorContext,
                telegramContext,
                exception,
                selection.RouteValues,
                cancellationToken).ConfigureAwait(false);
            var handled = result == TelegramErrorHandlingResult.Handled;

            if (logContext is { } context)
            {
                LogErrorHandlerCompleted(
                    _logger,
                    context.UpdateId,
                    context.UpdateType,
                    context.Handler,
                    context.Route,
                    context.ModuleName,
                    context.SceneName,
                    context.ExceptionType,
                    FormatErrorHandler(errorHandler),
                    handled);
            }

            if (handled)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether an exception represents cancellation requested for the current update.
    /// </summary>
    private static bool IsUpdateCancellation(Exception exception, CancellationToken cancellationToken)
    {
        return exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
    }

    /// <summary>
    /// Formats an error handler name for diagnostic logs.
    /// </summary>
    private static string FormatErrorHandler(TelegramErrorHandlerDescriptor handler)
    {
        return $"{handler.HandlerType.Name}.{handler.MethodName}";
    }

    /// <summary>
    /// Converts global callback auto-answer options to the descriptor used by the dispatcher.
    /// </summary>
    private static TelegramAutoAnswerCallbackDescriptor? CreateGlobalAutoAnswerDescriptor(
        TelegramAutoAnswerCallbackOptions? options)
    {
        return options is null
            ? null
            : new TelegramAutoAnswerCallbackDescriptor(options.Enabled, options.Text, options.ShowAlert);
    }

}
