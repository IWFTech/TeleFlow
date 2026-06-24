using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Annotations;
using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal.Handlers;

internal static class TelegramFilterEvaluator
{
    public static async ValueTask<bool> MatchesAsync(
        TelegramUpdateContext context,
        IReadOnlyList<TelegramFilterDescriptor> filters,
        IReadOnlyList<TelegramRoleRequirementDescriptor> roleRequirements,
        CancellationToken cancellationToken)
    {
        foreach (var filter in filters.Where(static filter => filter.CustomFilterType is null))
        {
            if (!MatchesBuiltInFilter(context, filter))
            {
                return false;
            }
        }

        foreach (var filter in filters.Where(static filter => filter.CustomFilterType is not null))
        {
            if (!await MatchesCustomFilterAsync(context, filter.CustomFilterType!, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        if (!await MatchesRoleRequirementsAsync(context, roleRequirements, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return true;
    }

    public static ValueTask<bool> MatchesAsync(
        TelegramUpdateContext context,
        IReadOnlyList<TelegramFilterDescriptor> filters,
        CancellationToken cancellationToken)
    {
        return MatchesAsync(context, filters, roleRequirements: [], cancellationToken);
    }

    private static bool MatchesBuiltInFilter(
        TelegramUpdateContext context,
        TelegramFilterDescriptor filter)
    {
        var facts = TelegramFilterContextFacts.From(context);

        return filter.Kind switch
        {
            TelegramFilterKind.ChatType => facts.Chat is not null &&
                                           filter.StringValues.Contains(facts.Chat.Type, StringComparer.Ordinal),
            TelegramFilterKind.ChatId => facts.Chat is not null &&
                                         filter.LongValues.Contains(facts.Chat.Id),
            TelegramFilterKind.ChatUsername => facts.Chat?.Username is { } username &&
                                               filter.StringValues.Contains(username, StringComparer.OrdinalIgnoreCase),
            TelegramFilterKind.MessageThreadId => facts.MessageThreadId is { } messageThreadId &&
                                                  filter.LongValues.Contains(messageThreadId),
            TelegramFilterKind.HasMessageThread => facts.MessageThreadId is not null,
            TelegramFilterKind.HasCallbackData => context is CallbackQueryContext callbackContext &&
                                                  !string.IsNullOrWhiteSpace(callbackContext.TelegramCallbackQuery.Data),
            TelegramFilterKind.CallbackDataPrefix => context is CallbackQueryContext callbackContext &&
                                                     callbackContext.TelegramCallbackQuery.Data is { } data &&
                                                     filter.StringValues.Any(prefix => data.StartsWith(prefix, StringComparison.Ordinal)),
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
            TelegramFilterKind.FromUser => message.From is not null && filter.LongValues.Contains(message.From.Id),
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
            TelegramFilterKind.FromBot => message.From is not null &&
                                          GetFirstStringValue(filter) is { } expected &&
                                          bool.TryParse(expected, out var expectedValue) &&
                                          message.From.IsBot == expectedValue,
            TelegramFilterKind.FromPremiumUser => message.From?.IsPremium == true,
            TelegramFilterKind.IsReply => message.ReplyToMessage is not null,
            TelegramFilterKind.ReplyToBot => message.ReplyToMessage?.From?.IsBot == true,
            _ => false
        };
    }

    private static async ValueTask<bool> MatchesCustomFilterAsync(
        TelegramUpdateContext context,
        Type filterType,
        CancellationToken cancellationToken)
    {
        var filter = context.Services.GetRequiredService(filterType);

        if (filter is ITelegramFilter<MessageContext> messageFilter &&
            context is MessageContext messageContext)
        {
            return await messageFilter.MatchesAsync(messageContext, cancellationToken).ConfigureAwait(false);
        }

        if (filter is ITelegramFilter<CallbackQueryContext> callbackFilter &&
            context is CallbackQueryContext callbackContext)
        {
            return await callbackFilter.MatchesAsync(callbackContext, cancellationToken).ConfigureAwait(false);
        }

        if (filter is ITelegramFilter<ChatMemberUpdatedContext> chatMemberFilter &&
            context is ChatMemberUpdatedContext chatMemberContext)
        {
            return await chatMemberFilter.MatchesAsync(chatMemberContext, cancellationToken).ConfigureAwait(false);
        }

        if (filter is ITelegramFilter<TelegramUpdateContext> updateFilter)
        {
            return await updateFilter.MatchesAsync(context, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"Telegram filter '{filterType.FullName}' must implement ITelegramFilter<TelegramUpdateContext>, ITelegramFilter<MessageContext>, ITelegramFilter<CallbackQueryContext>, or ITelegramFilter<ChatMemberUpdatedContext>.");
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
