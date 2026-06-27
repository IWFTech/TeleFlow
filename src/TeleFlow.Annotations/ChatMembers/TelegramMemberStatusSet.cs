namespace TeleFlow.Annotations;
/// <summary>
/// Telegram chat member statuses used for role checks and member transition routes.
/// </summary>
[Flags]
public enum TelegramMemberStatusSet
{
    /// <summary>
    /// Chat creator status.
    /// </summary>
    Creator = 1 << 0,

    /// <summary>
    /// Chat administrator status.
    /// </summary>
    Administrator = 1 << 1,

    /// <summary>
    /// Ordinary chat member status.
    /// </summary>
    Member = 1 << 2,

    /// <summary>
    /// Restricted user who is still a chat member.
    /// </summary>
    RestrictedMember = 1 << 3,

    /// <summary>
    /// Restricted user who is not currently a chat member.
    /// </summary>
    RestrictedNotMember = 1 << 4,

    /// <summary>
    /// User who left the chat.
    /// </summary>
    Left = 1 << 5,

    /// <summary>
    /// User banned from the chat.
    /// </summary>
    Banned = 1 << 6,

    /// <summary>
    /// Creator or administrator.
    /// </summary>
    IsAdmin = Creator | Administrator,

    /// <summary>
    /// Any status that represents an active chat member.
    /// </summary>
    IsMember = Creator | Administrator | Member | RestrictedMember,

    /// <summary>
    /// Any status that represents a non-member.
    /// </summary>
    IsNotMember = Left | Banned | RestrictedNotMember,

    /// <summary>
    /// All known Telegram member statuses.
    /// </summary>
    Any = Creator | Administrator | Member | RestrictedMember | RestrictedNotMember | Left | Banned
}
