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
dotnet add package IWF.TeleFlow.Telegram.LongPolling --prerelease
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

Default schema-decode policy работает fail-closed: валидный Telegram update,
который не удалось декодировать, останавливает polling и не попадает в
transient retry backoff. Для availability gateway зарегистрируй
`ITelegramUpdateDecodeFailurePolicy`, которая durable-сохраняет
`RawPayloadJson` и возвращает `Skip` только после успешного commit. Одна policy
работает для raw/framework long polling и webhooks. Полный контракт описан в
[контракте ошибок update](../reference/update-failure-contract.md).

## Raw webhooks

Установка:

```bash
dotnet add package IWF.TeleFlow.Telegram.Webhooks --prerelease
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

Невалидный JSON отклоняется как invalid request. Синтаксически валидный update
с известным `update_id`, но несовместимой schema попадает в общую decode policy.
`Stop` возвращает `500`; `Skip` возвращает `200` только после успешного
завершения policy.

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
