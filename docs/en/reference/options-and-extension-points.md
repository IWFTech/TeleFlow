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
});
```

Use this for client-only applications.

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
services.AddDefaultUpdateRateLimiting();
services.AddUpdateRateLimiter<MyLimiter>();
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
