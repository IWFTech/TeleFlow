using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TeleFlow.Annotations;
using TeleFlow.Framework.Callbacks;
using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal.Handlers;

/// <summary>
/// Matches incoming Telegram contexts to registered handler routes and binds route
/// values or typed callback payloads before the dispatcher invokes a handler.
/// </summary>
internal sealed partial class TelegramHandlerSelector
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyRouteValues =
        new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(StringComparer.Ordinal));
    private static readonly ConcurrentDictionary<Type, CallbackPayloadDeserializer> CallbackPayloadDeserializers = new();

    private readonly TelegramHandlerTable _table;
    private readonly TelegramBotIdentity _botIdentity;
    private readonly ILogger<TelegramHandlerSelector> _logger;

    public TelegramHandlerSelector(
        TelegramHandlerTable table,
        TelegramBotIdentity botIdentity,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(botIdentity);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _table = table;
        _botIdentity = botIdentity;
        _logger = loggerFactory.CreateLogger<TelegramHandlerSelector>();
    }

    public bool HasStatefulHandlers => _table.HasStatefulHandlers;

    public async ValueTask<TelegramRouteSelection?> SelectMessageHandlerAsync(
        MessageContext context,
        string? currentState,
        CancellationToken cancellationToken)
    {
        var selection = await SelectMessageHandlerAsync(
            context,
            _table.CommandHandlerCandidates,
            currentState,
            cancellationToken).ConfigureAwait(false);

        if (selection is not null)
        {
            return selection;
        }

        return await SelectMessageHandlerAsync(
            context,
            _table.MessageHandlerCandidates,
            currentState,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<TelegramRouteSelection?> SelectCallbackHandlerAsync(
        CallbackQueryContext context,
        string? currentState,
        CancellationToken cancellationToken)
    {
        if (HasCurrentState(currentState))
        {
            var state = currentState!;
            var statefulPayloadSelection = await SelectCallbackHandlerPassAsync(
                context,
                _table.CallbackHandlerCandidates.GetStatefulTypedCandidates(state),
                cancellationToken).ConfigureAwait(false);

            if (statefulPayloadSelection is not null)
            {
                return statefulPayloadSelection;
            }

            var statefulRawSelection = await SelectCallbackHandlerPassAsync(
                context,
                _table.CallbackHandlerCandidates.GetStatefulRawCandidates(state),
                cancellationToken).ConfigureAwait(false);

            if (statefulRawSelection is not null)
            {
                return statefulRawSelection;
            }
        }

        var statelessPayloadSelection = await SelectCallbackHandlerPassAsync(
            context,
            _table.CallbackHandlerCandidates.StatelessTyped,
            cancellationToken).ConfigureAwait(false);

        if (statelessPayloadSelection is not null)
        {
            return statelessPayloadSelection;
        }

        return await SelectCallbackHandlerPassAsync(
            context,
            _table.CallbackHandlerCandidates.StatelessRaw,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<TelegramRouteSelection?> SelectChatMemberHandlerAsync(
        ChatMemberUpdatedContext context,
        TelegramRouteKind updateRouteKind,
        string? currentState,
        CancellationToken cancellationToken)
    {
        if (HasCurrentState(currentState))
        {
            var state = currentState!;
            var statefulSelection = await SelectChatMemberHandlerPassAsync(
                context,
                _table.ChatMemberHandlerCandidates.GetStatefulCandidates(state),
                updateRouteKind,
                cancellationToken).ConfigureAwait(false);

            if (statefulSelection is not null)
            {
                return statefulSelection;
            }
        }

        return await SelectChatMemberHandlerPassAsync(
            context,
            _table.ChatMemberHandlerCandidates.Stateless,
            updateRouteKind,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<TelegramRouteSelection?> SelectMessageHandlerAsync(
        MessageContext context,
        TelegramHandlerCandidateSet candidates,
        string? currentState,
        CancellationToken cancellationToken)
    {
        if (HasCurrentState(currentState))
        {
            var state = currentState!;
            var statefulSelection = await SelectMessageHandlerPassAsync(
                context,
                candidates.GetStatefulCandidates(state),
                cancellationToken).ConfigureAwait(false);

            if (statefulSelection is not null)
            {
                return statefulSelection;
            }
        }

        return await SelectMessageHandlerPassAsync(
            context,
            candidates.Stateless,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<TelegramRouteSelection?> SelectMessageHandlerPassAsync(
        MessageContext context,
        IReadOnlyList<TelegramHandlerCandidate> candidates,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var route = candidate.Route;

            if (!TryMatchRoute(context.TelegramMessage, route, out var routeValues))
            {
                continue;
            }

            if (!await TelegramFilterEvaluator.MatchesAsync(
                    context,
                    candidate.Filters,
                    cancellationToken).ConfigureAwait(false))
            {
                LogRejectedByFiltersIfEnabled(context, candidate, route);
                continue;
            }

            return new TelegramRouteSelection(candidate.Handler, route, routeValues, callbackPayload: null);
        }

        return null;
    }

    private async ValueTask<TelegramRouteSelection?> SelectCallbackHandlerPassAsync(
        CallbackQueryContext context,
        IReadOnlyList<TelegramHandlerCandidate> candidates,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var route = candidate.Route;

            if (route.CallbackPayloadType is null)
            {
                if (!await TelegramFilterEvaluator.MatchesAsync(
                        context,
                        candidate.Filters,
                        cancellationToken).ConfigureAwait(false))
                {
                    LogRejectedByFiltersIfEnabled(context, candidate, route);
                    continue;
                }

                return new TelegramRouteSelection(candidate.Handler, route, EmptyRouteValues, callbackPayload: null);
            }

            if (string.IsNullOrWhiteSpace(context.TelegramCallbackQuery.Data))
            {
                continue;
            }

            object? callbackPayload;

            try
            {
                if (!TryDeserializeCallbackPayload(
                        context,
                        route.CallbackPayloadType,
                        out callbackPayload))
                {
                    continue;
                }
            }
            catch (CallbackDataRouteDeserializationException exception)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    LogCallbackDataDeserializationFailed(
                        _logger,
                        exception,
                        context.Update.UpdateId,
                        exception.PayloadType.FullName ?? exception.PayloadType.Name,
                        TelegramUpdateLogFormatter.FormatHandler(candidate.Handler),
                        TelegramUpdateLogFormatter.FormatRoute(route),
                        exception.PayloadByteCount);
                }

                continue;
            }

            if (!await TelegramFilterEvaluator.MatchesAsync(
                    context,
                    candidate.Filters,
                    cancellationToken).ConfigureAwait(false))
            {
                LogRejectedByFiltersIfEnabled(context, candidate, route);
                continue;
            }

            return new TelegramRouteSelection(candidate.Handler, route, EmptyRouteValues, callbackPayload);
        }

        return null;
    }

    private async ValueTask<TelegramRouteSelection?> SelectChatMemberHandlerPassAsync(
        ChatMemberUpdatedContext context,
        IReadOnlyList<TelegramHandlerCandidate> candidates,
        TelegramRouteKind updateRouteKind,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var route = candidate.Route;

            if (route.RouteKind != updateRouteKind)
            {
                continue;
            }

            if (!MatchesChatMemberTransition(context, route))
            {
                continue;
            }

            if (!await TelegramFilterEvaluator.MatchesAsync(
                    context,
                    candidate.Filters,
                    cancellationToken).ConfigureAwait(false))
            {
                LogRejectedByFiltersIfEnabled(context, candidate, route);
                continue;
            }

            return new TelegramRouteSelection(candidate.Handler, route, EmptyRouteValues, callbackPayload: null);
        }

        return null;
    }

    private void LogRejectedByFiltersIfEnabled(
        TelegramUpdateContext context,
        TelegramHandlerCandidate candidate,
        TelegramRouteDescriptor route)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        LogHandlerRejectedByFilters(
            _logger,
            context.Update.UpdateId,
            TelegramUpdateLogFormatter.GetUpdateType(context.Update),
            TelegramUpdateLogFormatter.FormatHandler(candidate.Handler),
            TelegramUpdateLogFormatter.FormatRoute(route));
    }

    private bool TryMatchRoute(
        Message message,
        TelegramRouteDescriptor route,
        out IReadOnlyDictionary<string, object?> routeValues)
    {
        routeValues = EmptyRouteValues;

        return route.RouteKind switch
        {
            TelegramRouteKind.MessageAny => MatchesTextFilters(message, route),
            TelegramRouteKind.TextExact => MatchesTextFilters(message, route),
            TelegramRouteKind.TextTemplate => TryMatchTextPattern(message.Text, route, isTemplate: true, out routeValues),
            TelegramRouteKind.TextRegex => TryMatchTextPattern(message.Text, route, isTemplate: false, out routeValues),
            TelegramRouteKind.CommandExact => TryGetCommandBody(message.Text, route, out var body, out var isPrefixLess) &&
                                               TryMatchExactCommand(body, route, isPrefixLess),
            TelegramRouteKind.CommandTemplate => TryGetCommandBody(message.Text, route, out var body, out _) &&
                                                 TryMatchCommandPattern(body, route, isTemplate: true, out routeValues),
            TelegramRouteKind.CommandRegex => TryGetCommandBody(message.Text, route, out var body, out _) &&
                                              TryMatchCommandPattern(body, route, isTemplate: false, out routeValues),
            _ => false
        };
    }

    private static bool HasCurrentState(string? currentState)
    {
        return !string.IsNullOrWhiteSpace(currentState);
    }

    private static bool TryDeserializeCallbackPayload(
        CallbackQueryContext context,
        Type payloadType,
        out object? payload)
    {
        var data = context.TelegramCallbackQuery.Data!;

        if (context.CallbackData is ICallbackDataRouteDeserializer routeDeserializer)
        {
            try
            {
                return routeDeserializer.TryDeserializeForRoute(payloadType, data, out payload);
            }
            catch (Exception exception) when (IsCallbackPayloadNoMatchException(exception))
            {
                payload = null;
                return false;
            }
        }

        var deserializer = CallbackPayloadDeserializers.GetOrAdd(payloadType, CreateCallbackPayloadDeserializer);

        try
        {
            payload = deserializer(context.CallbackData, data);
            return true;
        }
        catch (Exception exception) when (IsCallbackPayloadNoMatchException(exception))
        {
            payload = null;
            return false;
        }
    }

    private static bool IsCallbackPayloadNoMatchException(Exception exception)
    {
        return exception is JsonException or FormatException or OverflowException;
    }

    private static CallbackPayloadDeserializer CreateCallbackPayloadDeserializer(Type payloadType)
    {
        var deserializeMethod = typeof(TelegramHandlerSelector)
            .GetMethod(nameof(DeserializeCallbackPayload), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(payloadType);

        return deserializeMethod.CreateDelegate<CallbackPayloadDeserializer>();
    }

    private static object? DeserializeCallbackPayload<TPayload>(
        ICallbackDataSerializer serializer,
        string data)
    {
        return serializer.Deserialize<TPayload>(data);
    }

    private bool TryGetCommandBody(
        string? text,
        TelegramRouteDescriptor route,
        out string commandBody,
        out bool isPrefixLess)
    {
        commandBody = string.Empty;
        isPrefixLess = false;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        switch (route.CommandPolicy.PrefixMode)
        {
            case CommandPrefixMode.Required:
                return TryGetPrefixedCommandBody(text, route, out commandBody);

            case CommandPrefixMode.Optional:
                if (TryGetPrefixedCommandBody(text, route, out commandBody))
                {
                    return true;
                }

                isPrefixLess = true;
                return TryGetPrefixLessCommandBody(text, out commandBody);

            case CommandPrefixMode.NoPrefix:
                isPrefixLess = true;
                return TryGetPrefixLessCommandBody(text, out commandBody);

            default:
                return false;
        }
    }

    private bool TryGetPrefixedCommandBody(
        string text,
        TelegramRouteDescriptor route,
        out string commandBody)
    {
        commandBody = string.Empty;

        foreach (var prefix in route.CommandPolicy.Prefixes)
        {
            if (!text.StartsWith(prefix, route.CommandPolicy.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                continue;
            }

            commandBody = text[prefix.Length..];

            if (route.CommandPolicy.AllowSpaceAfterPrefix)
            {
                commandBody = commandBody.TrimStart(' ', '\t');
            }

            if (prefix == "/")
            {
                if (!TryTrimSlashCommandBotMention(commandBody, out commandBody))
                {
                    return false;
                }
            }

            return !string.IsNullOrWhiteSpace(commandBody);
        }

        return false;
    }

    private static bool TryGetPrefixLessCommandBody(
        string text,
        out string commandBody)
    {
        commandBody = text;

        return !string.IsNullOrWhiteSpace(commandBody);
    }

    private bool TryTrimSlashCommandBotMention(
        string commandBody,
        out string trimmedCommandBody)
    {
        var tokenEnd = commandBody.IndexOfAny([' ', '\t', '\r', '\n']);
        var token = tokenEnd < 0 ? commandBody : commandBody[..tokenEnd];
        var mentionIndex = token.IndexOf('@', StringComparison.Ordinal);

        if (mentionIndex < 0)
        {
            trimmedCommandBody = commandBody;
            return true;
        }

        if (!_botIdentity.MatchesMention(token.AsSpan()[(mentionIndex + 1)..]))
        {
            trimmedCommandBody = string.Empty;
            return false;
        }

        var trimmedToken = token[..mentionIndex];

        trimmedCommandBody = tokenEnd < 0
            ? trimmedToken
            : trimmedToken + commandBody[tokenEnd..];
        return true;
    }

    private static bool TryMatchExactCommand(
        string commandBody,
        TelegramRouteDescriptor route,
        bool isPrefixLess)
    {
        if (string.IsNullOrWhiteSpace(route.Pattern))
        {
            return false;
        }

        if (isPrefixLess)
        {
            return string.Equals(
                TelegramCommandTextNormalizer.Normalize(commandBody.Trim()),
                route.Pattern,
                route.CommandPolicy.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        var tokenEnd = commandBody.IndexOfAny([' ', '\t', '\r', '\n']);
        var token = tokenEnd < 0 ? commandBody : commandBody[..tokenEnd];

        return string.Equals(
            TelegramCommandTextNormalizer.Normalize(token),
            route.Pattern,
            route.CommandPolicy.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static bool TryMatchTextPattern(
        string? text,
        TelegramRouteDescriptor route,
        bool isTemplate,
        out IReadOnlyDictionary<string, object?> routeValues)
    {
        routeValues = EmptyRouteValues;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return TryMatchPattern(text, route, isTemplate, route.CommandPolicy.IgnoreCase, out routeValues);
    }

    private static bool TryMatchCommandPattern(
        string commandBody,
        TelegramRouteDescriptor route,
        bool isTemplate,
        out IReadOnlyDictionary<string, object?> routeValues)
    {
        var value = isTemplate
            ? TelegramCommandTextNormalizer.Normalize(commandBody)
            : commandBody;

        return TryMatchPattern(value, route, isTemplate, route.CommandPolicy.IgnoreCase, out routeValues);
    }

    private static bool TryMatchPattern(
        string value,
        TelegramRouteDescriptor route,
        bool isTemplate,
        bool ignoreCase,
        out IReadOnlyDictionary<string, object?> routeValues)
    {
        routeValues = EmptyRouteValues;

        if (string.IsNullOrWhiteSpace(route.Pattern))
        {
            return false;
        }

        var regex = route.Matcher.Regex ?? (isTemplate
            ? TelegramTemplateRouteParser.BuildRegex(route.Pattern, ignoreCase)
            : new Regex(route.Pattern, TelegramTemplateRouteParser.GetRegexOptions(ignoreCase)));
        var match = regex.Match(value);

        if (!match.Success)
        {
            return false;
        }

        return TryBindRouteValues(route, match, out routeValues);
    }

    private static bool TryBindRouteValues(
        TelegramRouteDescriptor route,
        Match match,
        out IReadOnlyDictionary<string, object?> routeValues)
    {
        routeValues = EmptyRouteValues;

        if (route.RouteValues.Count == 0)
        {
            return true;
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var valueDescriptor in route.RouteValues)
        {
            var group = match.Groups[valueDescriptor.Name];

            if (!group.Success)
            {
                if (!valueDescriptor.IsOptional)
                {
                    return false;
                }

                values[valueDescriptor.Name] = null;
                continue;
            }

            if (!TryConvertRouteValue(valueDescriptor, group.Value, out var convertedValue))
            {
                return false;
            }

            values[valueDescriptor.Name] = convertedValue;
        }

        routeValues = values;
        return true;
    }

    private static bool TryConvertRouteValue(
        TelegramRouteValueDescriptor descriptor,
        string value,
        out object? convertedValue)
    {
        if (descriptor.ValueType == typeof(string))
        {
            convertedValue = value;
            return true;
        }

        if (descriptor.ValueType == typeof(int) &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            convertedValue = intValue;
            return true;
        }

        if (descriptor.ValueType == typeof(long) &&
            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            convertedValue = longValue;
            return true;
        }

        convertedValue = null;

        if (descriptor.ValueType == typeof(int) ||
            descriptor.ValueType == typeof(long))
        {
            return false;
        }

        throw new InvalidOperationException(
            $"Route value '{descriptor.Name}' uses unsupported type {descriptor.ValueType.Name}.");
    }

    private static bool MatchesTextFilters(Message message, TelegramRouteDescriptor route)
    {
        var textFilters = route.TextFilters;

        for (var index = 0; index < textFilters.Count; index++)
        {
            if (!textFilters[index].Matches(message.Text))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesChatMemberTransition(
        ChatMemberUpdatedContext context,
        TelegramRouteDescriptor route)
    {
        if (route.ChatMemberTransitions.Count == 0)
        {
            return true;
        }

        var oldStatus = TelegramChatMemberClassifier.GetStatus(context.OldChatMember);
        var newStatus = TelegramChatMemberClassifier.GetStatus(context.NewChatMember);
        var transitions = route.ChatMemberTransitions;

        for (var index = 0; index < transitions.Count; index++)
        {
            var transition = transitions[index];

            if ((transition.OldStatus & oldStatus) != 0 &&
                (transition.NewStatus & newStatus) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private delegate object? CallbackPayloadDeserializer(ICallbackDataSerializer serializer, string data);
}
