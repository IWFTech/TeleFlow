# Options и extension points

Здесь перечислены основные configuration и replacement APIs.

## Telegram bot options

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

`AddTelegramBot(...)` конфигурирует framework-level Telegram services и underlying client.

## Client options

```csharp
services.AddTelegramClient(options =>
{
    options.Token = token;
    options.BotUsername = "my_bot";
    options.BaseUrl = "https://api.telegram.org";
    options.RetryAfter = TelegramRetryAfterPolicy.Default;
});
```

Используй это для client-only applications.

`RetryAfter` управляет bounded automatic handling для Telegram `429` responses. Default policy retry-ит один короткий retry-after response и бросает `TelegramRetryAfterException`, когда настроенные границы превышены.

## Long polling options

```csharp
builder.Services.AddLongPolling(options =>
{
    options.TimeoutSeconds = 30;
    options.Limit = 100;
    options.AllowedUpdates = TelegramAllowedUpdates.Auto;
});
```

## Webhook options

```csharp
builder.Services.AddWebhook(options =>
{
    options.Path = "/telegram/webhook";
    options.SecretToken = secret;
});
```

## Raw long polling options

```csharp
var options = new TelegramRawLongPollingOptions
{
    TimeoutSeconds = 30,
    Limit = 100,
    AllowedUpdates = ["message", "callback_query"]
};
```

## Raw webhook options

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
services.AddStateStorageKeyBuilder<MyStateStorageKeyBuilder>();
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

## Рекомендации

Replacement points заменяй только когда приложение владеет behavior. Держи defaults, пока нет конкретной причины их менять.

Lifecycle tasks используй для короткой startup/shutdown работы приложения. Не регистрируй `ITeleFlowStartupTask` или `ITeleFlowShutdownTask` напрямую в `IServiceCollection`: TeleFlow не использует direct lifecycle service registrations и понятно падает во время application build. Регистрируй tasks через `AddTeleFlowStartupTask<TTask>()` и `AddTeleFlowShutdownTask<TTask>()`, чтобы они создавались из правильного lifecycle scope.
