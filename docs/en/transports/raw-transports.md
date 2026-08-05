# Raw Transports

Raw transports are for applications that want Telegram `Update` values directly and do not want TeleFlow handler routing.

Use raw transports when:

- another service owns dispatching;
- updates are pushed into a queue;
- you are building a gateway;
- you want only the Telegram client and transport mechanics.

## Raw Long Polling

Install:

```bash
dotnet add package IWF.TeleFlow.Telegram.LongPolling --prerelease
```

Register:

```csharp
services.AddTelegramClient(options => options.Token = token);
services.AddTelegramLongPollingClient();
```

Run:

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

Or enumerate acknowledged updates:

```csharp
await foreach (var polled in polling.GetUpdatesAsync(cancellationToken: cancellationToken))
{
    await HandleUpdateAsync(polled.Update, cancellationToken);
    await polled.AcknowledgeAsync(cancellationToken);
}
```

`RunAsync(...)` advances the Telegram offset only after the handler completes successfully. `GetUpdatesAsync(...)` advances the offset only after `AcknowledgeAsync(...)`. If the update is not acknowledged, TeleFlow fails before requesting the next update. This prevents accidental update loss in queue/gateway scenarios.

The default schema-decode policy is fail-closed: a valid Telegram update that
cannot be decoded stops polling and does not enter transient retry backoff. To
keep a gateway available, register an
`ITelegramUpdateDecodeFailurePolicy` that durably quarantines
`RawPayloadJson` and returns `Skip` only after the commit succeeds. The same
policy is shared with raw and framework webhooks. See the
[update failure contract](../reference/update-failure-contract.md).

## Raw Webhooks

Install:

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

Malformed JSON is rejected as an invalid request. A syntactically valid update
with a known `update_id` but an incompatible schema enters the shared decode
policy. `Stop` returns `500`; `Skip` returns `200` only after the policy
completes successfully.

## Raw vs Framework

Choose framework transports when you want:

- handlers;
- routing attributes;
- filters;
- callbacks;
- state;
- error handlers.

Choose raw transports when you want:

- `Update` values;
- your own dispatcher;
- your own queueing model;
- a thin Telegram gateway.
