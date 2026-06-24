using System.ComponentModel;
using TeleFlow.Annotations;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure Telegram role requirement metadata emitted by TeleFlow source generators.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TelegramGeneratedRoleRequirementDescriptor
{
    public TelegramGeneratedRoleRequirementDescriptor(TelegramMemberStatusSet allowedStatuses)
    {
        AllowedStatuses = allowedStatuses;
    }

    public TelegramMemberStatusSet AllowedStatuses { get; }
}
