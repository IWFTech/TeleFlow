# Long Polling

Long polling is the simplest way to run a Telegram bot. The application repeatedly calls Telegram `getUpdates` and processes returned updates.

## Framework Long Polling

Install:

```bash
dotnet add package IWF.TeleFlow.Framework.LongPolling --prerelease
```

Register:

```csharp
builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();
```

Configure:

```csharp
builder.Services.AddLongPolling(options =>
{
    options.TimeoutSeconds = 30;
    options.Limit = 100;
    options.AllowedUpdates = TelegramAllowedUpdates.Auto;
    options.Backoff.MinDelay = TimeSpan.FromSeconds(1);
    options.Backoff.MaxDelay = TimeSpan.FromSeconds(5);
});
```

`TelegramAllowedUpdates.Auto` resolves allowed update types from registered handlers. This keeps polling narrower without requiring beginners to maintain update type strings manually.

Allowed updates modes:

```csharp
options.AllowedUpdates = TelegramAllowedUpdates.Auto;
options.AllowedUpdates = TelegramAllowedUpdates.All;
options.AllowedUpdates = TelegramAllowedUpdates.Only(
    TelegramUpdateType.Message,
    TelegramUpdateType.CallbackQuery);
```

Long polling retries transient `getUpdates` failures with configurable backoff. If the Telegram client surfaces `TelegramRetryAfterException`, polling waits for the Telegram-provided retry delay instead of using the generic backoff delay. Handler failures are not swallowed. The offset advances only after successful update processing.

The exact acknowledgement outcome for handled route failures, middleware
failures, cancellation, and automatic callback answers is defined by the
[update failure and delivery contract](../reference/update-failure-contract.md).

At `Information`, framework long polling writes one event when an update is
received, one when a Telegram handler is matched or no handler matches, and one
after the update is acknowledged. This gives production logs a complete update
path without enabling `Debug`. `Debug` adds candidate-rejection diagnostics and
route and handler timing. The `Information` path does not start timing scopes
or calculate elapsed durations.

## When To Use Long Polling

Use long polling when:

- developing locally;
- running a small worker;
- deploying to an environment without inbound public HTTP;
- keeping infrastructure simple is more important than webhook push delivery.

## Operational Notes

Long polling applications are long-running processes. Run them under a host that restarts the process on failure and exposes logs.

For production:

- pass cancellation from the host;
- keep handlers idempotent where possible;
- avoid process-local state for multi-instance deployments;
- prefer one active long polling worker per bot token unless you intentionally build coordination around it.

## Drop Pending Updates

TeleFlow long polling consumes updates from Telegram according to the polling offset maintained by the polling client. If the bot was offline, Telegram may return pending updates. Design startup behavior intentionally when operating production bots.

The current public docs do not claim a `drop_pending_updates` option. If that API is added later, it should be documented here with exact semantics.
