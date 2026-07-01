using System.Globalization;

namespace TeleFlow.Telegram.Internal.Options;

/// <summary>
/// Stores Telegram-specific state-key settings resolved during Telegram framework registration,
/// before updates reach state middleware and handler dispatch.
/// </summary>
internal sealed record class TelegramStateKeyOptions(long? BotId)
{
    public static TelegramStateKeyOptions FromToken(string token)
    {
        return TryResolveBotId(token, out var botId)
            ? new TelegramStateKeyOptions(botId)
            : new TelegramStateKeyOptions(BotId: null);
    }

    private static bool TryResolveBotId(string token, out long botId)
    {
        botId = default;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var separatorIndex = token.IndexOf(':', StringComparison.Ordinal);

        return separatorIndex > 0 &&
            long.TryParse(
                token.AsSpan(0, separatorIndex),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out botId);
    }
}
