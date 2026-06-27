namespace TeleFlow.Annotations;

/// <summary>
/// Predefined Telegram chat member transitions for common membership events.
/// </summary>
public enum TelegramMemberTransition
{
    /// <summary>
    /// User joined or became an active chat member.
    /// </summary>
    Join,

    /// <summary>
    /// User left or stopped being an active chat member.
    /// </summary>
    Leave,

    /// <summary>
    /// User gained administrator-level status.
    /// </summary>
    Promoted,

    /// <summary>
    /// User lost administrator-level status.
    /// </summary>
    Demoted
}
