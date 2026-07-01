using TeleFlow.Annotations;

namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed record TelegramChatMemberTransitionDescriptor(
    TelegramMemberStatusSet OldStatus,
    TelegramMemberStatusSet NewStatus);
