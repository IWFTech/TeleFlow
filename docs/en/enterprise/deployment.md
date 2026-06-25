# Deployment

TeleFlow deployment is normal .NET deployment. The framework does not require a custom process model.

Choose the transport first:

- long polling: one long-running worker owns `getUpdates`;
- webhooks: ASP.NET Core receives HTTPS requests from Telegram;
- raw transports: your application owns dispatching, queueing, and acknowledgement semantics.

## Long Polling Worker

Long polling is easiest to deploy as a worker process:

```csharp
var builder = TeleFlowApplication.CreateBuilder(args);

builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();

await using var app = builder.Build();
await app.RunAsync();
```

Operational rules:

- run one active long polling worker per bot token unless you intentionally coordinate workers;
- make handlers idempotent where possible;
- pass cancellation from the host;
- use durable state storage before multi-instance or restart-sensitive workflows;
- keep logs searchable by update id, chat id, handler, and exception type.

## Webhook App

Webhook deployment is an ASP.NET Core app:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddWebhook(options =>
{
    options.Path = "/telegram/webhook";
    options.SecretToken = webhookSecret;
});

var app = builder.Build();
app.MapTelegramWebhook();

await app.RunAsync();
```

Operational rules:

- expose HTTPS;
- keep the webhook path stable;
- configure Telegram webhook URL outside the request handler;
- validate `SecretToken`;
- keep request handling short;
- move long-running work behind your own queue when needed.

## Configuration

Production tokens should come from the platform, not from committed files:

```csharp
options.Token = configuration["Telegram:BotToken"]
    ?? throw new InvalidOperationException("Telegram:BotToken is not configured.");
```

Use environment variables, secret managers, CI/CD variables, Docker secrets, Kubernetes secrets, or your cloud provider's secret storage.

See [Configuration and secrets](../getting-started/configuration.md).

## Docker

A TeleFlow bot can be published like any .NET app:

```bash
dotnet publish -c Release -o publish
```

For containers:

- pass the token through environment or secrets;
- do not bake secrets into the image;
- send logs to stdout/stderr;
- configure graceful shutdown;
- avoid memory storage for workflows that must survive container restarts.

Long polling containers should usually run as one replica per bot token. Webhook containers can scale horizontally if your handlers and storage are safe for concurrency.

## systemd

For a small Linux host, run long polling under `systemd`:

```ini
[Service]
WorkingDirectory=/opt/teleflow-bot
ExecStart=/usr/bin/dotnet /opt/teleflow-bot/MyBot.dll
Restart=on-failure
Environment=Telegram__BotToken=123456:ABC
```

Keep secrets in an environment file with restricted permissions when possible.

## Kubernetes

For Kubernetes:

- use a `Deployment` for webhook apps;
- use one replica for long polling unless worker coordination exists;
- store tokens in `Secret`;
- expose webhooks through ingress with TLS;
- use readiness/liveness probes for ASP.NET Core webhook apps;
- use external storage for state.

## Pending Updates

Telegram can keep updates while the bot is offline. With long polling, pending updates can be delivered after restart.

Current public TeleFlow docs do not claim a `drop_pending_updates` option. Until such an API exists, treat startup behavior as an operational decision:

- handlers should tolerate duplicate or delayed user actions;
- critical flows should be idempotent;
- deployments should avoid long downtime;
- if old updates are harmful for your product, plan a release task for explicit drop-pending support before production launch.

## Release Checklist

Before production:

- generated registration is used or reflection use is documented;
- `IWF.TeleFlow.Generators` is private;
- token and webhook secret are not committed;
- transport choice is documented;
- state storage is suitable for the deployment topology;
- logs and errors are observable;
- CI runs build and tests;
- smoke test covers `/start`, one callback, one state flow, and one Telegram API call.

