using TeleFlow.Framework.Updates;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public sealed class TelegramUpdatePayload : IUpdatePayload
{
    public TelegramUpdatePayload(Update update)
    {
        ArgumentNullException.ThrowIfNull(update);

        Update = update;
    }

    public Update Update { get; }
}
