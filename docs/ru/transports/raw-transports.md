# Raw transports

Raw transports нужны приложениям, которым нужны Telegram `Update` values напрямую, без TeleFlow handler routing.

Используй raw transports, когда:

- другой сервис владеет dispatching;
- updates отправляются в queue;
- ты строишь gateway;
- нужен только Telegram client и transport mechanics.

## Raw long polling

Установка:

```bash
dotnet add package IWF.TeleFlow.Telegram.LongPolling
```

Регистрация:

```csharp
services.AddTelegramClient(options => options.Token = token);
services.AddTelegramLongPollingClient();
```

Запуск:

```csharp
var polling = provider.GetRequiredService<ITelegramLongPollingClient>();

await polling.RunAsync(
    async (update, ct) =>
    {
        await HandleUpdateAsync(update, ct);
    },
    new TelegramRawLongPollingOptions
    {
        TimeoutSeconds = 30,
        Limit = 100,
        AllowedUpdates = ["message", "callback_query"]
    },
    cancellationToken);
```

Или enumerate acknowledged updates:

```csharp
await foreach (var polled in polling.GetUpdatesAsync(cancellationToken: cancellationToken))
{
    await HandleUpdateAsync(polled.Update, cancellationToken);
    await polled.AcknowledgeAsync(cancellationToken);
}
```

`RunAsync(...)` продвигает Telegram offset только после успешного завершения handler. `GetUpdatesAsync(...)` продвигает offset только после `AcknowledgeAsync(...)`. Если update не acknowledged, TeleFlow падает до запроса следующего update. Это защищает от случайной потери updates в queue/gateway сценариях.

## Raw webhooks

Установка:

```bash
dotnet add package IWF.TeleFlow.Telegram.Webhooks
```

Map endpoint:

```csharp
app.MapTelegramWebhook(
    "/telegram/raw",
    async (update, bot, ct) =>
    {
        await HandleUpdateAsync(update, ct);
        return Results.Ok();
    },
    options =>
    {
        options.SecretToken = webhookSecret;
    });
```

## Raw vs framework

Framework transports выбирай, когда нужны:

- handlers;
- routing attributes;
- filters;
- callbacks;
- state;
- error handlers.

Raw transports выбирай, когда нужны:

- `Update` values;
- свой dispatcher;
- свой queueing model;
- тонкий Telegram gateway.
