using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using TeleFlow.Core.Callbacks;
using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed class TelegramHandlerSelector
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyRouteValues =
        new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(StringComparer.Ordinal));
    private static readonly ConcurrentDictionary<Type, CallbackPayloadDeserializer> CallbackPayloadDeserializers = new();

    private readonly TelegramHandlerTable _table;

    public TelegramHandlerSelector(TelegramHandlerTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        _table = table;
    }

    public bool HasStatefulHandlers => _table.HasStatefulHandlers;

    public async ValueTask<TelegramRouteSelection?> SelectMessageHandlerAsync(
        MessageContext context,
        string? currentState,
        CancellationToken cancellationToken)
    {
        var selection = await SelectMessageHandlerAsync(
            context,
            _table.CommandHandlers,
            currentState,
            cancellationToken).ConfigureAwait(false);

        if (selection is not null)
        {
            return selection;
        }

        return await SelectMessageHandlerAsync(
            context,
            _table.MessageHandlers,
            currentState,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<TelegramRouteSelection?> SelectCallbackHandlerAsync(
        CallbackQueryContext context,
        string? currentState,
        CancellationToken cancellationToken)
    {
        foreach (var handler in PrioritizeCallbackHandlers(_table.CallbackHandlers, currentState))
        {
            var route = handler.Route;

            if (route.CallbackPayloadType is null)
            {
                if (!await TelegramFilterEvaluator.MatchesAsync(
                        context,
                        route.Filters,
                        route.RoleRequirements,
                        cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                return new TelegramRouteSelection(handler, route, EmptyRouteValues, callbackPayload: null);
            }

            if (string.IsNullOrWhiteSpace(context.TelegramCallbackQuery.Data))
            {
                continue;
            }

            if (TryDeserializeCallbackPayload(
                    context,
                    route.CallbackPayloadType,
                    out var callbackPayload))
            {
                if (!await TelegramFilterEvaluator.MatchesAsync(
                        context,
                        route.Filters,
                        route.RoleRequirements,
                        cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                return new TelegramRouteSelection(handler, route, EmptyRouteValues, callbackPayload);
            }
        }

        return null;
    }

    public async ValueTask<TelegramRouteSelection?> SelectChatMemberHandlerAsync(
        ChatMemberUpdatedContext context,
        TelegramRouteKind updateRouteKind,
        string? currentState,
        CancellationToken cancellationToken)
    {
        foreach (var handler in PrioritizeHandlersByState(_table.ChatMemberHandlers, currentState))
        {
            var route = handler.Route;

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
                    route.Filters,
                    route.RoleRequirements,
                    cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            return new TelegramRouteSelection(handler, route, EmptyRouteValues, callbackPayload: null);
        }

        return null;
    }

    private static async ValueTask<TelegramRouteSelection?> SelectMessageHandlerAsync(
        MessageContext context,
        IReadOnlyList<TelegramHandlerDescriptor> handlers,
        string? currentState,
        CancellationToken cancellationToken)
    {
        foreach (var handler in PrioritizeHandlersByState(handlers, currentState))
        {
            var route = handler.Route;

            if (!TryMatchRoute(context.TelegramMessage, route, out var routeValues))
            {
                continue;
            }

            if (!await TelegramFilterEvaluator.MatchesAsync(
                    context,
                    route.Filters,
                    route.RoleRequirements,
                    cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            return new TelegramRouteSelection(handler, route, routeValues, callbackPayload: null);
        }

        return null;
    }

    private static bool TryMatchRoute(
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
            TelegramRouteKind.CommandExact => TryGetCommandBody(message.Text, route, out var body) &&
                                               TryMatchExactCommand(body, route),
            TelegramRouteKind.CommandTemplate => TryGetCommandBody(message.Text, route, out var body) &&
                                                 TryMatchCommandPattern(body, route, isTemplate: true, out routeValues),
            TelegramRouteKind.CommandRegex => TryGetCommandBody(message.Text, route, out var body) &&
                                              TryMatchCommandPattern(body, route, isTemplate: false, out routeValues),
            _ => false
        };
    }

    private static IEnumerable<TelegramHandlerDescriptor> PrioritizeHandlersByState(
        IReadOnlyList<TelegramHandlerDescriptor> handlers,
        string? currentState)
    {
        if (!string.IsNullOrWhiteSpace(currentState))
        {
            foreach (var handler in handlers)
            {
                if (MatchesState(handler, currentState))
                {
                    yield return handler;
                }
            }
        }

        foreach (var handler in handlers)
        {
            if (handler.States.Count == 0)
            {
                yield return handler;
            }
        }
    }

    private static IEnumerable<TelegramHandlerDescriptor> PrioritizeCallbackHandlers(
        IReadOnlyList<TelegramHandlerDescriptor> handlers,
        string? currentState)
    {
        if (!string.IsNullOrWhiteSpace(currentState))
        {
            foreach (var handler in handlers)
            {
                if (handler.CallbackPayloadType is not null && MatchesState(handler, currentState))
                {
                    yield return handler;
                }
            }

            foreach (var handler in handlers)
            {
                if (handler.CallbackPayloadType is null && MatchesState(handler, currentState))
                {
                    yield return handler;
                }
            }
        }

        foreach (var handler in handlers)
        {
            if (handler.CallbackPayloadType is not null && handler.States.Count == 0)
            {
                yield return handler;
            }
        }

        foreach (var handler in handlers)
        {
            if (handler.CallbackPayloadType is null && handler.States.Count == 0)
            {
                yield return handler;
            }
        }
    }

    private static bool MatchesState(TelegramHandlerDescriptor handler, string currentState)
    {
        return handler.States.Any(state => string.Equals(state, currentState, StringComparison.Ordinal));
    }

    private static bool TryDeserializeCallbackPayload(
        CallbackQueryContext context,
        Type payloadType,
        out object? payload)
    {
        var deserializer = CallbackPayloadDeserializers.GetOrAdd(payloadType, CreateCallbackPayloadDeserializer);

        try
        {
            payload = deserializer(context.CallbackData, context.TelegramCallbackQuery.Data!);
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

    private static bool TryGetCommandBody(
        string? text,
        TelegramRouteDescriptor route,
        out string commandBody)
    {
        commandBody = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

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
                commandBody = TrimSlashCommandBotMention(commandBody);
            }

            return !string.IsNullOrWhiteSpace(commandBody);
        }

        return false;
    }

    private static string TrimSlashCommandBotMention(string commandBody)
    {
        var tokenEnd = commandBody.IndexOfAny([' ', '\t', '\r', '\n']);
        var token = tokenEnd < 0 ? commandBody : commandBody[..tokenEnd];
        var mentionIndex = token.IndexOf('@', StringComparison.Ordinal);

        if (mentionIndex < 0)
        {
            return commandBody;
        }

        var trimmedToken = token[..mentionIndex];

        return tokenEnd < 0
            ? trimmedToken
            : trimmedToken + commandBody[tokenEnd..];
    }

    private static bool TryMatchExactCommand(
        string commandBody,
        TelegramRouteDescriptor route)
    {
        if (string.IsNullOrWhiteSpace(route.Pattern))
        {
            return false;
        }

        var tokenEnd = commandBody.IndexOfAny([' ', '\t', '\r', '\n']);
        var token = tokenEnd < 0 ? commandBody : commandBody[..tokenEnd];

        return string.Equals(
            token,
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
        return TryMatchPattern(commandBody, route, isTemplate, route.CommandPolicy.IgnoreCase, out routeValues);
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
        return route.TextFilters.Count == 0 ||
               route.TextFilters.All(filter => filter.Matches(message.Text));
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

        return route.ChatMemberTransitions.Any(transition =>
            (transition.OldStatus & oldStatus) != 0 &&
            (transition.NewStatus & newStatus) != 0);
    }

    private delegate object? CallbackPayloadDeserializer(ICallbackDataSerializer serializer, string data);
}
