using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public sealed class TelegramBotDefaults
{
    public TelegramParseMode? ParseMode { get; set; }

    public LinkPreviewOptions? LinkPreviewOptions { get; set; }

    public bool? DisableNotification { get; set; }

    public bool? ProtectContent { get; set; }
}
