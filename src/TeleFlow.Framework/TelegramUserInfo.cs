using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public sealed class TelegramUserInfo
{
    internal TelegramUserInfo(User telegramUser)
    {
        ArgumentNullException.ThrowIfNull(telegramUser);

        TelegramUser = telegramUser;
    }

    public User TelegramUser { get; }

    public string FullName => TelegramUserNameFormatter.GetFullName(TelegramUser.FirstName, TelegramUser.LastName);
}
