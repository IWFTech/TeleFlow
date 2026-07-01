using TeleFlow.Annotations;

namespace TeleFlow.Telegram.Internal;

internal static class TelegramMemberStatusSetValidator
{
    private const TelegramMemberStatusSet KnownMask = TelegramMemberStatusSet.Any;

    public static bool IsValid(TelegramMemberStatusSet value)
    {
        return value != 0 && (value & ~KnownMask) == 0;
    }
}
