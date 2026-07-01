using TeleFlow.Annotations;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

internal static class TelegramChatMemberClassifier
{
    public static TelegramMemberStatusSet GetStatus(ChatMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (member.TryGetChatMemberOwner(out _))
        {
            return TelegramMemberStatusSet.Creator;
        }

        if (member.TryGetChatMemberAdministrator(out _))
        {
            return TelegramMemberStatusSet.Administrator;
        }

        if (member.TryGetChatMemberMember(out _))
        {
            return TelegramMemberStatusSet.Member;
        }

        if (member.TryGetChatMemberRestricted(out var restricted))
        {
            return restricted!.IsMember
                ? TelegramMemberStatusSet.RestrictedMember
                : TelegramMemberStatusSet.RestrictedNotMember;
        }

        if (member.TryGetChatMemberLeft(out _))
        {
            return TelegramMemberStatusSet.Left;
        }

        if (member.TryGetChatMemberBanned(out _))
        {
            return TelegramMemberStatusSet.Banned;
        }

        throw new InvalidOperationException("The Telegram chat member union does not contain a supported case.");
    }

    public static User GetUser(ChatMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (member.TryGetChatMemberOwner(out var owner))
        {
            return owner!.User;
        }

        if (member.TryGetChatMemberAdministrator(out var administrator))
        {
            return administrator!.User;
        }

        if (member.TryGetChatMemberMember(out var memberCase))
        {
            return memberCase!.User;
        }

        if (member.TryGetChatMemberRestricted(out var restricted))
        {
            return restricted!.User;
        }

        if (member.TryGetChatMemberLeft(out var left))
        {
            return left!.User;
        }

        if (member.TryGetChatMemberBanned(out var banned))
        {
            return banned!.User;
        }

        throw new InvalidOperationException("The Telegram chat member union does not contain a supported case.");
    }
}
