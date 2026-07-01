using TeleFlow.Annotations;

namespace TeleFlow.Telegram.Internal.Handlers;

internal static class TelegramMemberTransitionFacts
{
    private const TelegramMemberStatusSet PromotedOldStatus =
        TelegramMemberStatusSet.Member |
        TelegramMemberStatusSet.RestrictedMember |
        TelegramMemberStatusSet.RestrictedNotMember |
        TelegramMemberStatusSet.Left |
        TelegramMemberStatusSet.Banned;

    public static TelegramChatMemberTransitionDescriptor Map(TelegramMemberTransition transition)
    {
        return transition switch
        {
            TelegramMemberTransition.Join => new TelegramChatMemberTransitionDescriptor(
                TelegramMemberStatusSet.IsNotMember,
                TelegramMemberStatusSet.IsMember),
            TelegramMemberTransition.Leave => new TelegramChatMemberTransitionDescriptor(
                TelegramMemberStatusSet.IsMember,
                TelegramMemberStatusSet.IsNotMember),
            TelegramMemberTransition.Promoted => new TelegramChatMemberTransitionDescriptor(
                PromotedOldStatus,
                TelegramMemberStatusSet.IsAdmin),
            TelegramMemberTransition.Demoted => new TelegramChatMemberTransitionDescriptor(
                TelegramMemberStatusSet.IsAdmin,
                PromotedOldStatus),
            _ => throw new InvalidOperationException($"Unsupported Telegram member transition '{transition}'.")
        };
    }
}
