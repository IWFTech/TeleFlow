# Options And Extension Points

This page lists the main configuration and replacement APIs.

## Telegram Bot Options

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.BotUsername = "my_bot";
    options.BaseUrl = "https://api.telegram.org";
    options.Defaults.ParseMode = TelegramParseMode.Html;
    options.RetryAfter = TelegramRetryAfterPolicy.Default;
    options.RoleFilter.CacheEnabled = true;
    options.RoleFilter.CacheTtl = TimeSpan.FromSeconds(30);
});
```

`AddTelegramBot(...)` configures framework-level Telegram services and the underlying client.

## Client Options

```csharp
services.AddTelegramClient(options =>
{
    options.Token = token;
    options.BotUsername = "my_bot";
    options.BaseUrl = "https://api.telegram.org";
    options.RetryAfter = TelegramRetryAfterPolicy.Default;
});
```

Use this for client-only applications.

`RetryAfter` controls bounded automatic handling of Telegram `429` responses. The default policy retries one short retry-after response and throws `TelegramRetryAfterException` when the configured bounds are exceeded.

## Long Polling Options

```csharp
builder.Services.AddLongPolling(options =>
{
    options.TimeoutSeconds = 30;
    options.Limit = 100;
    options.AllowedUpdates = TelegramAllowedUpdates.Auto;
});
```

## Webhook Options

```csharp
builder.Services.AddWebhook(options =>
{
    options.Path = "/telegram/webhook";
    options.SecretToken = secret;
});
```

## Raw Long Polling Options

```csharp
var options = new TelegramRawLongPollingOptions
{
    TimeoutSeconds = 30,
    Limit = 100,
    AllowedUpdates = ["message", "callback_query"]
};
```

## Raw Webhook Options

```csharp
using Microsoft.AspNetCore.Http;

app.MapTelegramWebhook(
    "/telegram/raw",
    handler,
    options =>
    {
        options.SecretToken = secret;
        options.InvalidPayloadStatusCode = StatusCodes.Status400BadRequest;
        options.SecretTokenFailureStatusCode = StatusCodes.Status401Unauthorized;
    });
```

## Replacement APIs

Core:

```csharp
services.AddUpdateDispatcher<MyDispatcher>();
services.AddUpdateSource<MySource>();
services.AddUpdateMiddleware<MyMiddleware>();
services.AddSingletonUpdateMiddleware<MyStatelessMiddleware>();
services.AddDefaultUpdateRateLimiting();
services.AddUpdateRateLimiter<MyLimiter>();
services.AddTeleFlowStartupTask<MyStartupTask>();
services.AddTeleFlowShutdownTask<MyShutdownTask>();
```

State:

```csharp
services.AddStateStore<MyStateStore>();
services.AddStateDataStore<MyStateDataStore>();
services.AddStateDataSerializer<MySerializer>();
services.AddStateHistoryStore<MyHistoryStore>();
services.AddStateKeyFactory<MyStateKeyFactory>();
```

Telegram framework:

```csharp
services.AddCallbackDataSerializer<MyCallbackSerializer>();
services.AddTelegramChatMemberStatusResolver<MyResolver>();
services.AddTelegramChatMemberStatusCache<MyCache>();
services.AddAutoCallbackAnswer();
```

Telegram client:

```csharp
services.AddTelegramClient<MyClient>();
services.AddTelegramTransport<MyTransport>();
services.AddTelegramRequestExecutor<MyExecutor>();
services.AddTelegramHttpTransport(httpClient);
services.AddTelegramJsonOptions(options => { });
services.AddDeepLinkPayloadSerializer<MySerializer>();
```

## Guidance

Replace extension points only when the application owns the behavior. Keep defaults until there is a concrete reason to change them.

Use lifecycle tasks for short application startup and shutdown work. Do not register `ITeleFlowStartupTask` or `ITeleFlowShutdownTask` directly in `IServiceCollection`; TeleFlow ignores direct lifecycle service registrations and fails clearly during application build. Register tasks through `AddTeleFlowStartupTask<TTask>()` and `AddTeleFlowShutdownTask<TTask>()` so they are resolved from the correct lifecycle scope.
