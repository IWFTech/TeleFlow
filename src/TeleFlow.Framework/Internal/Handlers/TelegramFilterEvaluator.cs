using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Annotations;
using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal.Handlers;

internal static class TelegramFilterEvaluator
{
    public static async ValueTask<bool> MatchesAsync(
        TelegramUpdateContext context,
        TelegramRouteFilterPlan filterPlan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filterPlan);

        var builtInFilters = filterPlan.BuiltInFilters;
        var facts = builtInFilters.Count > 0
            ? TelegramFilterContextFacts.From(context)
            : default;

        for (var index = 0; index < builtInFilters.Count; index++)
        {
            var filter = builtInFilters[index];

            if (!MatchesBuiltInFilter(context, facts, filter))
            {
                return false;
            }
        }

        var customFilters = filterPlan.CustomFilters;

        for (var index = 0; index < customFilters.Count; index++)
        {
            var customFilter = customFilters[index];

            if (!await customFilter.MatchesAsync(context, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        if (!await MatchesRoleRequirementsAsync(context, filterPlan.RoleRequirements, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesBuiltInFilter(
        TelegramUpdateContext context,
        TelegramFilterContextFacts facts,
        TelegramFilterDescriptor filter)
    {
        return filter.Kind switch
        {
            TelegramFilterKind.ChatType => facts.DestinationChat is not null &&
                                           ContainsString(
                                               filter.StringValues,
                                               facts.DestinationChat.Type,
                                               StringComparison.Ordinal),
            TelegramFilterKind.ChatId => facts.DestinationChat is not null &&
                                         ContainsLong(filter.LongValues, facts.DestinationChat.Id),
            TelegramFilterKind.ChatUsername => facts.DestinationChat?.Username is { } username &&
                                               ContainsString(
                                                   filter.StringValues,
                                                   username,
                                                   StringComparison.OrdinalIgnoreCase),
            TelegramFilterKind.FromUser => facts.SenderUser is not null &&
                                           ContainsLong(filter.LongValues, facts.SenderUser.Id),
            TelegramFilterKind.FromHuman => facts.SenderUser is { IsBot: false },
            TelegramFilterKind.FromBot => facts.SenderUser is not null &&
                                          GetFirstStringValue(filter) is { } expected &&
                                          bool.TryParse(expected, out var expectedValue) &&
                                          facts.SenderUser.IsBot == expectedValue,
            TelegramFilterKind.FromPremiumUser => facts.SenderUser?.IsPremium == true,
            TelegramFilterKind.SenderChatType => facts.SenderChat is not null &&
                                                 ContainsString(
                                                     filter.StringValues,
                                                     facts.SenderChat.Type,
                                                     StringComparison.Ordinal),
            TelegramFilterKind.MessageThreadId => facts.MessageThreadId is { } messageThreadId &&
                                                  ContainsLong(filter.LongValues, messageThreadId),
            TelegramFilterKind.HasMessageThread => facts.MessageThreadId is not null,
            TelegramFilterKind.HasCallbackData => context is CallbackQueryContext callbackContext &&
                                                  !string.IsNullOrWhiteSpace(callbackContext.TelegramCallbackQuery.Data),
            TelegramFilterKind.CallbackDataPrefix => context is CallbackQueryContext callbackContext &&
                                                     callbackContext.TelegramCallbackQuery.Data is { } data &&
                                                     StartsWithAnyPrefix(data, filter.StringValues),
            _ => context is MessageContext messageContext &&
                 MatchesMessageBuiltInFilter(messageContext.TelegramMessage, filter)
        };
    }

    private static bool MatchesMessageBuiltInFilter(
        Message message,
        TelegramFilterDescriptor filter)
    {
        return filter.Kind switch
        {
            TelegramFilterKind.HasText => !string.IsNullOrWhiteSpace(message.Text),
            TelegramFilterKind.HasPhoto => message.Photo is { Count: > 0 },
            TelegramFilterKind.HasDocument => message.Document is not null,
            TelegramFilterKind.HasCaption => !string.IsNullOrWhiteSpace(message.Caption),
            TelegramFilterKind.HasVideo => message.Video is not null,
            TelegramFilterKind.HasAnimation => message.Animation is not null,
            TelegramFilterKind.HasAudio => message.Audio is not null,
            TelegramFilterKind.HasVoice => message.Voice is not null,
            TelegramFilterKind.HasVideoNote => message.VideoNote is not null,
            TelegramFilterKind.HasSticker => message.Sticker is not null,
            TelegramFilterKind.HasContact => message.Contact is not null,
            TelegramFilterKind.HasLocation => message.Location is not null,
            TelegramFilterKind.HasVenue => message.Venue is not null,
            TelegramFilterKind.HasPoll => message.Poll is not null,
            TelegramFilterKind.HasDice => message.Dice is not null,
            TelegramFilterKind.IsReply => message.ReplyToMessage is not null,
            TelegramFilterKind.ReplyToBot => message.ReplyToMessage?.From?.IsBot == true,
            _ => false
        };
    }

    private static async ValueTask<bool> MatchesRoleRequirementsAsync(
        TelegramUpdateContext context,
        IReadOnlyList<TelegramRoleRequirementDescriptor> roleRequirements,
        CancellationToken cancellationToken)
    {
        if (roleRequirements.Count == 0)
        {
            return true;
        }

        if (!TelegramRoleFilterIdentityResolver.TryResolve(context, out var identity))
        {
            return false;
        }

        var options = context.Services.GetRequiredService<TelegramRoleFilterOptions>();
        var status = await ResolveStatusAsync(context, identity, options, cancellationToken).ConfigureAwait(false);

        if (status is null)
        {
            return false;
        }

        if (!TelegramMemberStatusSetValidator.IsValid(status.Value))
        {
            throw new InvalidOperationException("Telegram role resolver returned an invalid member status set.");
        }

        foreach (var requirement in roleRequirements)
        {
            if ((status.Value & requirement.AllowedStatuses) == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static async ValueTask<TelegramMemberStatusSet?> ResolveStatusAsync(
        TelegramUpdateContext context,
        TelegramRoleFilterIdentity identity,
        TelegramRoleFilterOptions options,
        CancellationToken cancellationToken)
    {
        if (options.CacheEnabled)
        {
            var cache = context.Services.GetRequiredService<ITelegramChatMemberStatusCache>();
            var cachedStatus = await cache.GetAsync(
                identity.ChatId,
                identity.UserId,
                cancellationToken).ConfigureAwait(false);

            if (cachedStatus is not null)
            {
                return cachedStatus;
            }

            var resolvedStatus = await ResolveUncachedStatusAsync(context, identity, cancellationToken).ConfigureAwait(false);

            if (resolvedStatus is not null)
            {
                await cache.SetAsync(
                    identity.ChatId,
                    identity.UserId,
                    resolvedStatus.Value,
                    options.CacheTtl,
                    cancellationToken).ConfigureAwait(false);
            }

            return resolvedStatus;
        }

        return await ResolveUncachedStatusAsync(context, identity, cancellationToken).ConfigureAwait(false);
    }

    private static string? GetFirstStringValue(TelegramFilterDescriptor filter)
    {
        return filter.StringValues.Count > 0
            ? filter.StringValues[0]
            : null;
    }

    private static bool ContainsString(
        IReadOnlyList<string> values,
        string value,
        StringComparison comparison)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, comparison))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsLong(
        IReadOnlyList<long> values,
        long value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] == value)
            {
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithAnyPrefix(
        string value,
        IReadOnlyList<string> prefixes)
    {
        for (var index = 0; index < prefixes.Count; index++)
        {
            if (value.StartsWith(prefixes[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ValueTask<TelegramMemberStatusSet?> ResolveUncachedStatusAsync(
        TelegramUpdateContext context,
        TelegramRoleFilterIdentity identity,
        CancellationToken cancellationToken)
    {
        var resolver = context.Services.GetRequiredService<ITelegramChatMemberStatusResolver>();
        return resolver.ResolveAsync(
            context,
            identity.ChatId,
            identity.UserId,
            cancellationToken);
    }
}
