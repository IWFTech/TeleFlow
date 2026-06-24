using Microsoft.AspNetCore.Http;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Webhooks;

public delegate Task<IResult> TelegramRawWebhookHandler(
    Update update,
    ITelegramClient bot,
    CancellationToken cancellationToken);
