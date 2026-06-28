using Microsoft.Extensions.Logging;
using TeleFlow.Core.Dispatching;
using TeleFlow.Core.States;
using TeleFlow.Core.Updates;
using TeleFlow.Telegram.Internal;

namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed partial class TelegramHandlerDispatcher : IUpdateDispatcher
{
    private readonly TelegramHandlerSelector _selector;
    private readonly TelegramErrorHandlerDescriptor[] _errorHandlers;
    private readonly TimeProvider _timeProvider;
    private readonly TelegramAutoAnswerCallbackDescriptor? _globalAutoAnswerCallback;
    private readonly ILogger<TelegramHandlerDispatcher> _logger;

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
        _errorHandlers = errorHandlers.ToArray();
        _timeProvider = timeProvider;
        _globalAutoAnswerCallback = CreateGlobalAutoAnswerDescriptor(autoAnswerCallbackOptions.LastOrDefault());
        _logger = loggerFactory.CreateLogger<TelegramHandlerDispatcher>();
    }

    public async Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Payload is not TelegramUpdatePayload payload)
        {
            return;
        }

        var debugEnabled = _logger.IsEnabled(LogLevel.Debug);
        var errorEnabled = _logger.IsEnabled(LogLevel.Error);
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
            if (errorEnabled)
            {
                if (handlerTimingEnabled)
                {
                    var handlerElapsed = _timeProvider.GetElapsedTime(handlerStarted);
                    var timing = requestTimingScope!.CreateSummary(_timeProvider, handlerElapsed);

                    LogHandlerFailedWithTiming(
                        _logger,
                        exception,
                        payload.Update.UpdateId,
                        GetUpdateType(),
                        GetHandlerName(),
                        GetRouteName(),
                        selection.Handler.ModuleName ?? string.Empty,
                        selection.Handler.SceneName ?? string.Empty,
                        exception.GetType().FullName ?? exception.GetType().Name,
                        handlerElapsed.TotalMilliseconds,
                        timing.RequestCount,
                        timing.RequestElapsedMilliseconds,
                        timing.HandlerLogicElapsedMilliseconds);
                }
                else
                {
                    LogHandlerFailed(
                        _logger,
                        exception,
                        payload.Update.UpdateId,
                        GetUpdateType(),
                        GetHandlerName(),
                        GetRouteName(),
                        selection.Handler.ModuleName ?? string.Empty,
                        selection.Handler.SceneName ?? string.Empty,
                        exception.GetType().FullName ?? exception.GetType().Name);
                }
            }

            if (_errorHandlers.Length > 0 &&
                await TryHandleErrorAsync(
                    selection,
                    telegramContext,
                    exception,
                    payload.Update.UpdateId,
                    GetUpdateType(),
                    GetHandlerName(),
                    GetRouteName(),
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

    private static async ValueTask<string?> GetCurrentStateAsync(
        UpdateContext context,
        CancellationToken cancellationToken)
    {
        return context.TryGetState(out var state)
            ? await state.GetAsync(cancellationToken).ConfigureAwait(false)
            : null;
    }

    private double GetElapsedMilliseconds(long startingTimestamp)
    {
        return _timeProvider.GetElapsedTime(startingTimestamp).TotalMilliseconds;
    }

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

    private async ValueTask<bool> TryHandleErrorAsync(
        TelegramRouteSelection selection,
        TelegramUpdateContext telegramContext,
        Exception exception,
        long updateId,
        string updateType,
        string handlerName,
        string routeName,
        CancellationToken cancellationToken)
    {
        if (_errorHandlers.Length == 0)
        {
            return false;
        }

        var errorContext = new TelegramErrorContext(
            exception,
            telegramContext,
            selection.Handler.HandlerType,
            selection.Handler.MethodName,
            selection.Handler.ModuleName,
            selection.Handler.SceneName,
            selection.RouteValues);

        foreach (var errorHandler in GetErrorHandlerCandidates(selection, telegramContext, exception))
        {
            var result = await TelegramErrorHandlerInvoker.InvokeAsync(
                errorHandler,
                errorContext,
                telegramContext,
                exception,
                selection.RouteValues,
                cancellationToken).ConfigureAwait(false);
            var handled = result == TelegramErrorHandlingResult.Handled;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                LogErrorHandlerCompleted(
                    _logger,
                    updateId,
                    updateType,
                    handlerName,
                    routeName,
                    selection.Handler.ModuleName ?? string.Empty,
                    selection.Handler.SceneName ?? string.Empty,
                    exception.GetType().FullName ?? exception.GetType().Name,
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

    private IEnumerable<TelegramErrorHandlerDescriptor> GetErrorHandlerCandidates(
        TelegramRouteSelection selection,
        TelegramUpdateContext telegramContext,
        Exception exception)
    {
        if (selection.Handler.ModuleName is { } moduleName)
        {
            foreach (var candidate in RankErrorHandlerCandidates(
                         selection.RouteValues,
                         telegramContext,
                         exception,
                         _errorHandlers.Where(handler => string.Equals(handler.ModuleName, moduleName, StringComparison.Ordinal))))
            {
                yield return candidate;
            }
        }

        foreach (var candidate in RankErrorHandlerCandidates(
                     selection.RouteValues,
                     telegramContext,
                     exception,
                     _errorHandlers.Where(static handler => handler.ModuleName is null)))
        {
            yield return candidate;
        }
    }

    private static IEnumerable<TelegramErrorHandlerDescriptor> RankErrorHandlerCandidates(
        IReadOnlyDictionary<string, object?> routeValues,
        TelegramUpdateContext telegramContext,
        Exception exception,
        IEnumerable<TelegramErrorHandlerDescriptor> candidates)
    {
        return candidates
            .Where(handler => IsCompatibleErrorHandler(handler, routeValues, telegramContext, exception))
            .OrderBy(handler => GetExceptionDistance(handler.ExceptionType, exception.GetType()))
            .ThenBy(static handler => handler.RegistrationOrder);
    }

    private static bool IsCompatibleErrorHandler(
        TelegramErrorHandlerDescriptor handler,
        IReadOnlyDictionary<string, object?> routeValues,
        TelegramUpdateContext telegramContext,
        Exception exception)
    {
        if (handler.ExceptionType is not null &&
            !handler.ExceptionType.IsAssignableFrom(exception.GetType()))
        {
            return false;
        }

        if (handler.TelegramContextType is not null &&
            !handler.TelegramContextType.IsInstanceOfType(telegramContext))
        {
            return false;
        }

        if (!HasCompatibleRouteValues(handler, routeValues))
        {
            return false;
        }

        return true;
    }

    private static bool HasCompatibleRouteValues(
        TelegramErrorHandlerDescriptor handler,
        IReadOnlyDictionary<string, object?> routeValues)
    {
        foreach (var parameter in handler.Parameters.Where(static parameter => parameter.Kind == TelegramErrorHandlerParameterKind.RouteValue))
        {
            if (string.IsNullOrWhiteSpace(parameter.Name) ||
                !routeValues.TryGetValue(parameter.Name, out var value))
            {
                return false;
            }

            if (value is null)
            {
                if (parameter.ParameterType.IsValueType && Nullable.GetUnderlyingType(parameter.ParameterType) is null)
                {
                    return false;
                }

                continue;
            }

            var underlyingParameterType = Nullable.GetUnderlyingType(parameter.ParameterType);

            if (underlyingParameterType is not null)
            {
                if (!underlyingParameterType.IsInstanceOfType(value))
                {
                    return false;
                }

                continue;
            }

            if (!parameter.ParameterType.IsInstanceOfType(value))
            {
                return false;
            }
        }

        return true;
    }

    private static int GetExceptionDistance(Type? handlerExceptionType, Type thrownExceptionType)
    {
        if (handlerExceptionType is null)
        {
            return int.MaxValue;
        }

        var distance = 0;
        var current = thrownExceptionType;

        while (current is not null)
        {
            if (current == handlerExceptionType)
            {
                return distance;
            }

            distance++;
            current = current.BaseType;
        }

        return int.MaxValue - 1;
    }

    private static bool IsUpdateCancellation(Exception exception, CancellationToken cancellationToken)
    {
        return exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
    }

    private static string FormatErrorHandler(TelegramErrorHandlerDescriptor handler)
    {
        return $"{handler.HandlerType.Name}.{handler.MethodName}";
    }

    private static TelegramAutoAnswerCallbackDescriptor? CreateGlobalAutoAnswerDescriptor(
        TelegramAutoAnswerCallbackOptions? options)
    {
        return options is null
            ? null
            : new TelegramAutoAnswerCallbackDescriptor(options.Enabled, options.Text, options.ShowAlert);
    }

}
