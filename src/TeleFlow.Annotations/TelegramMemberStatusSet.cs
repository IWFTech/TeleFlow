namespace TeleFlow.Annotations;

[Flags]
public enum TelegramMemberStatusSet
{
    Creator = 1 << 0,
    Administrator = 1 << 1,
    Member = 1 << 2,
    RestrictedMember = 1 << 3,
    RestrictedNotMember = 1 << 4,
    Left = 1 << 5,
    Banned = 1 << 6,
    IsAdmin = Creator | Administrator,
    IsMember = Creator | Administrator | Member | RestrictedMember,
    IsNotMember = Left | Banned | RestrictedNotMember,
    Any = Creator | Administrator | Member | RestrictedMember | RestrictedNotMember | Left | Banned
}
