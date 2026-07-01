# Webhooks

Webhooks позволяют Telegram пушить updates в ASP.NET Core endpoint.

## Framework webhooks

Установка:

```bash
dotnet add package IWF.TeleFlow.Framework.Webhooks --prerelease
dotnet add package IWF.TeleFlow.Generators --prerelease
```

Регистрация services:

```csharp
builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddWebhook(options =>
{
    options.Path = "/telegram/webhook";
    options.SecretToken = webhookSecret;
});
```

Endpoint mapping:

```csharp
var app = builder.Build();

app.MapTelegramWebhook();

await app.RunAsync();
```

`MapTelegramWebhook()` использует configured `TelegramWebhookOptions.Path` и передаёт incoming Telegram updates в TeleFlow update processor.

## Когда использовать webhooks

Webhooks подходят, когда:

- бот уже работает внутри ASP.NET Core;
- доступен public HTTPS endpoint;
- platform предпочитает HTTP-triggered workloads;
- ты хочешь, чтобы Telegram pushed updates instead of polling.

## Secret token

Используй `SecretToken`, когда webhook endpoint доступен публично:

```csharp
builder.Services.AddWebhook(options =>
{
    options.Path = "/telegram/webhook";
    options.SecretToken = configuration["Telegram:WebhookSecret"];
});
```

Raw webhook layer валидирует secret token и может отклонять invalid payloads.

## Deployment checklist

- Настраивай Telegram webhook URL вне request handler.
- Используй HTTPS.
- Держи webhook path стабильным.
- Установи и валидируй secret token.
- Отправляй ASP.NET Core и TeleFlow logs в одну observability platform.
- Держи request time коротким. Long-running work выноси за свою queue, если нужно.
