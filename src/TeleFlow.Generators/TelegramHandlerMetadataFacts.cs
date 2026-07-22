using System.Text;
using Microsoft.CodeAnalysis;

namespace TeleFlow.Generators;

internal enum TelegramHandlerMetadataRouteKind
{
    Command,
    Message,
    Callback,
    ChatMember
}

internal enum TelegramBuiltInFilterTarget
{
    Message,
    Callback,
    Chat,
    MessageThread,
    SenderUser,
    SenderChat
}

internal sealed record TelegramBuiltInFilterSpec(
    string AttributeMetadataName,
    string GeneratedKind,
    TelegramBuiltInFilterTarget Target,
    bool IsMarker);

internal static class TelegramChatTypeFacts
{
    public static bool IsKnown(int value)
    {
        return value is >= 0 and <= 3;
    }

    public static bool TryMapToTelegramValue(int value, out string telegramValue)
    {
        switch (value)
        {
            case 0:
                telegramValue = "private";
                return true;
            case 1:
                telegramValue = "group";
                return true;
            case 2:
                telegramValue = "supergroup";
                return true;
            case 3:
                telegramValue = "channel";
                return true;
            default:
                telegramValue = string.Empty;
                return false;
        }
    }
}

internal static class TelegramMemberStatusFacts
{
    public const int Creator = 1 << 0;
    public const int Administrator = 1 << 1;
    public const int Member = 1 << 2;
    public const int RestrictedMember = 1 << 3;
    public const int RestrictedNotMember = 1 << 4;
    public const int Left = 1 << 5;
    public const int Banned = 1 << 6;
    public const int IsAdmin = Creator | Administrator;
    public const int IsMember = Creator | Administrator | Member | RestrictedMember;
    public const int IsNotMember = Left | Banned | RestrictedNotMember;
    public const int Any =
        Creator |
        Administrator |
        Member |
        RestrictedMember |
        RestrictedNotMember |
        Left |
        Banned;

    private const int PromotedOldStatus =
        Member |
        RestrictedMember |
        RestrictedNotMember |
        Left |
        Banned;

    public static bool IsValid(int value)
    {
        return value != 0 && (value & ~Any) == 0;
    }

    public static bool TryMapTransition(
        int transition,
        out int oldStatus,
        out int newStatus)
    {
        switch (transition)
        {
            case 0:
                oldStatus = IsNotMember;
                newStatus = IsMember;
                return true;
            case 1:
                oldStatus = IsMember;
                newStatus = IsNotMember;
                return true;
            case 2:
                oldStatus = PromotedOldStatus;
                newStatus = IsAdmin;
                return true;
            case 3:
                oldStatus = IsAdmin;
                newStatus = PromotedOldStatus;
                return true;
            default:
                oldStatus = 0;
                newStatus = 0;
                return false;
        }
    }

    public static bool TryGetRoleRequirementMask(
        AttributeData attribute,
        out int allowedStatuses)
    {
        allowedStatuses = 0;

        if (attribute.ConstructorArguments.Length == 0)
        {
            return false;
        }

        TypedConstant argument = attribute.ConstructorArguments[0];

        if (argument.Kind == TypedConstantKind.Array)
        {
            foreach (TypedConstant value in argument.Values)
            {
                if (value.Value is not int status)
                {
                    return false;
                }

                allowedStatuses |= status;
            }

            return true;
        }

        if (argument.Value is int singleStatus)
        {
            allowedStatuses = singleStatus;
            return true;
        }

        return false;
    }
}

internal static class TelegramCallbackDataFacts
{
    public const int MaxCallbackDataBytes = 64;

    public static bool IsValidPayloadPrefix(string? prefix)
    {
        return prefix is not null &&
               !string.IsNullOrWhiteSpace(prefix) &&
               prefix.IndexOf(':') < 0 &&
               prefix.IndexOf('%') < 0 &&
               !prefix.Any(char.IsWhiteSpace) &&
               Encoding.UTF8.GetByteCount(prefix) <= MaxCallbackDataBytes;
    }

    public static bool IsSupportedFieldType(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_String or
                   SpecialType.System_Int32 or
                   SpecialType.System_Int64 or
                   SpecialType.System_Boolean ||
               type.TypeKind == TypeKind.Enum;
    }
}

internal static class TelegramBuiltInFilterFacts
{
    public static IReadOnlyList<TelegramBuiltInFilterSpec> All { get; } = new TelegramBuiltInFilterSpec[]
    {
        new(TelegramHandlerSymbols.ChatTypeAttribute, "ChatType", TelegramBuiltInFilterTarget.Chat, IsMarker: false),
        new(TelegramHandlerSymbols.SenderChatTypeAttribute, "SenderChatType", TelegramBuiltInFilterTarget.SenderChat, IsMarker: false),
        new(TelegramHandlerSymbols.ChatIdAttribute, "ChatId", TelegramBuiltInFilterTarget.Chat, IsMarker: false),
        new(TelegramHandlerSymbols.ChatUsernameAttribute, "ChatUsername", TelegramBuiltInFilterTarget.Chat, IsMarker: false),
        new(TelegramHandlerSymbols.FromUserAttribute, "FromUser", TelegramBuiltInFilterTarget.SenderUser, IsMarker: false),
        new(TelegramHandlerSymbols.HasTextAttribute, "HasText", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.HasPhotoAttribute, "HasPhoto", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.HasDocumentAttribute, "HasDocument", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.HasCaptionAttribute, "HasCaption", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.HasVideoAttribute, "HasVideo", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.HasAnimationAttribute, "HasAnimation", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.HasAudioAttribute, "HasAudio", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.HasVoiceAttribute, "HasVoice", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.HasVideoNoteAttribute, "HasVideoNote", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.HasStickerAttribute, "HasSticker", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.HasContactAttribute, "HasContact", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.HasLocationAttribute, "HasLocation", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.HasVenueAttribute, "HasVenue", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.HasPollAttribute, "HasPoll", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.HasDiceAttribute, "HasDice", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.FromBotAttribute, "FromBot", TelegramBuiltInFilterTarget.SenderUser, IsMarker: false),
        new(TelegramHandlerSymbols.FromPremiumUserAttribute, "FromPremiumUser", TelegramBuiltInFilterTarget.SenderUser, IsMarker: true),
        new(TelegramHandlerSymbols.IsReplyAttribute, "IsReply", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.ReplyToBotAttribute, "ReplyToBot", TelegramBuiltInFilterTarget.Message, IsMarker: true),
        new(TelegramHandlerSymbols.MessageThreadIdAttribute, "MessageThreadId", TelegramBuiltInFilterTarget.MessageThread, IsMarker: false),
        new(TelegramHandlerSymbols.HasMessageThreadAttribute, "HasMessageThread", TelegramBuiltInFilterTarget.MessageThread, IsMarker: true),
        new(TelegramHandlerSymbols.HasCallbackDataAttribute, "HasCallbackData", TelegramBuiltInFilterTarget.Callback, IsMarker: true),
        new(TelegramHandlerSymbols.CallbackDataPrefixAttribute, "CallbackDataPrefix", TelegramBuiltInFilterTarget.Callback, IsMarker: false)
    };

    public static IEnumerable<TelegramBuiltInFilterSpec> MarkerSpecs
    {
        get
        {
            return All.Where(static spec => spec.IsMarker);
        }
    }

    public static IEnumerable<AttributeData> GetAttributes(ISymbol symbol)
    {
        foreach (TelegramBuiltInFilterSpec spec in All)
        {
            foreach (AttributeData attribute in TelegramHandlerSymbols.GetAttributes(
                         symbol,
                         spec.AttributeMetadataName,
                         inherit: true))
            {
                yield return attribute;
            }
        }
    }

    public static bool TryGetSpec(
        AttributeData attribute,
        out TelegramBuiltInFilterSpec spec)
    {
        foreach (TelegramBuiltInFilterSpec candidate in All)
        {
            if (TelegramHandlerSymbols.IsAttribute(attribute, candidate.AttributeMetadataName))
            {
                spec = candidate;
                return true;
            }
        }

        spec = null!;
        return false;
    }

    public static bool TryGetSpecByGeneratedKind(
        string generatedKind,
        out TelegramBuiltInFilterSpec spec)
    {
        foreach (TelegramBuiltInFilterSpec candidate in All)
        {
            if (string.Equals(candidate.GeneratedKind, generatedKind, StringComparison.Ordinal))
            {
                spec = candidate;
                return true;
            }
        }

        spec = null!;
        return false;
    }

    public static bool SupportsRouteKind(
        TelegramBuiltInFilterTarget target,
        TelegramHandlerMetadataRouteKind routeKind)
    {
        return target switch
        {
            TelegramBuiltInFilterTarget.Chat => true,
            TelegramBuiltInFilterTarget.MessageThread => routeKind is TelegramHandlerMetadataRouteKind.Command or
                TelegramHandlerMetadataRouteKind.Message or
                TelegramHandlerMetadataRouteKind.Callback,
            TelegramBuiltInFilterTarget.Message => routeKind is TelegramHandlerMetadataRouteKind.Command or
                TelegramHandlerMetadataRouteKind.Message,
            TelegramBuiltInFilterTarget.SenderUser => routeKind is TelegramHandlerMetadataRouteKind.Command or
                TelegramHandlerMetadataRouteKind.Message or
                TelegramHandlerMetadataRouteKind.Callback,
            TelegramBuiltInFilterTarget.SenderChat => routeKind is TelegramHandlerMetadataRouteKind.Command or
                TelegramHandlerMetadataRouteKind.Message,
            TelegramBuiltInFilterTarget.Callback => routeKind == TelegramHandlerMetadataRouteKind.Callback,
            _ => false
        };
    }

    public static string GetInvalidFilterMessage(
        TelegramBuiltInFilterTarget target,
        TelegramHandlerMetadataRouteKind routeKind)
    {
        string routeName = routeKind switch
        {
            TelegramHandlerMetadataRouteKind.Callback => "callback",
            TelegramHandlerMetadataRouteKind.ChatMember => "chat member update",
            _ => "message"
        };

        return target switch
        {
            TelegramBuiltInFilterTarget.Chat => $"Chat filters cannot be used on {routeName} handlers.",
            TelegramBuiltInFilterTarget.MessageThread => $"Message thread filters cannot be used on {routeName} handlers.",
            TelegramBuiltInFilterTarget.Message => $"Message filters cannot be used on {routeName} handlers.",
            TelegramBuiltInFilterTarget.SenderUser => $"Sender user filters cannot be used on {routeName} handlers.",
            TelegramBuiltInFilterTarget.SenderChat => $"Sender chat filters cannot be used on {routeName} handlers.",
            TelegramBuiltInFilterTarget.Callback => $"Callback filters cannot be used on {routeName} handlers.",
            _ => $"Telegram filter cannot be used on {routeName} handlers."
        };
    }
}
