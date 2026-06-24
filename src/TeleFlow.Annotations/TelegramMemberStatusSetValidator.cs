namespace TeleFlow.Annotations;

internal static class TelegramMemberStatusSetValidator
{
    private const TelegramMemberStatusSet KnownMask = TelegramMemberStatusSet.Any;

    public static bool IsValid(TelegramMemberStatusSet value)
    {
        return value != 0 && (value & ~KnownMask) == 0;
    }

    public static TelegramMemberStatusSet Combine(IEnumerable<TelegramMemberStatusSet> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var result = (TelegramMemberStatusSet)0;

        foreach (var value in values)
        {
            result |= value;
        }

        return result;
    }
}
