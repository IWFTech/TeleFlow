# Webhooks

Webhooks let Telegram push updates to an ASP.NET Core endpoint.

## Framework Webhooks

Install:

```bash
dotnet add package IWF.TeleFlow.Framework.Webhooks --prerelease
dotnet add package IWF.TeleFlow.Generators --prerelease
```

Register services:

```csharp
builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddWebhook(options =>
{
    options.Path = "/telegram/webhook";
    options.SecretToken = webhookSecret;
});
```

Map endpoint:

```csharp
var app = builder.Build();

app.MapTelegramWebhook();

await app.RunAsync();
```

`MapTelegramWebhook()` uses the configured `TelegramWebhookOptions.Path` and forwards incoming Telegram updates to the TeleFlow update processor.

The endpoint returns `200 OK` only after the update processor completes
normally. The full handling and failure contract is documented in
[update failure and delivery contract](../reference/update-failure-contract.md).

## When To Use Webhooks

Use webhooks when:

- the bot already runs inside ASP.NET Core;
- a public HTTPS endpoint is available;
- your platform prefers HTTP-triggered workloads;
- you want Telegram to push updates instead of polling.

## Secret Token

Use `SecretToken` when exposing webhook endpoints publicly:

```csharp
builder.Services.AddWebhook(options =>
{
    options.Path = "/telegram/webhook";
    options.SecretToken = configuration["Telegram:WebhookSecret"];
});
```

The raw webhook layer validates the secret token and can reject invalid payloads.

## Deployment Checklist

- Configure Telegram webhook URL outside the request handler.
- Use HTTPS.
- Keep the webhook path stable.
- Set and validate secret token.
- Route logs from ASP.NET Core and TeleFlow to the same observability platform.
- Keep request time short. Move long-running work behind your own queue if needed.
