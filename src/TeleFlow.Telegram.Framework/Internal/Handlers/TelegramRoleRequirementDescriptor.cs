using TeleFlow.Annotations;

namespace TeleFlow.Telegram.Internal.Handlers;

internal sealed record TelegramRoleRequirementDescriptor(TelegramMemberStatusSet AllowedStatuses);
