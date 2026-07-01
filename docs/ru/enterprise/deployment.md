# Deployment

TeleFlow deployment - это обычный .NET deployment. Framework не требует custom process model.

Сначала выбери transport:

- long polling: один long-running worker владеет `getUpdates`;
- webhooks: ASP.NET Core принимает HTTPS requests от Telegram;
- raw transports: приложение само владеет dispatching, queueing и acknowledgement semantics.

## Long polling worker

Long polling проще всего deploy-ить как worker process. В обычном .NET worker используй `IWF.TeleFlow.Framework.Hosting`, чтобы Generic Host владел startup, shutdown, logging и cancellation:

```csharp
using Microsoft.Extensions.Hosting;
using TeleFlow.Framework.Hosting;
using TeleFlow.Telegram;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();
builder.Services.AddTeleFlowHostedService();

await builder.Build().RunAsync();
```

Hosted service создаёт TeleFlow application из host service provider. Он не создаёт второй DI container и не dispose-ит provider, которым владеет host.

Для маленького console process без Generic Host запускай TeleFlow application напрямую:

```csharp
var builder = TeleFlowApplication.CreateBuilder(args);

builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();

await using var app = builder.Build();
await app.RunAsync();
```

Operational rules:

- запускай один active long polling worker на bot token, если специально не строишь worker coordination;
- по возможности делай handlers idempotent;
- передавай cancellation от host;
- используй durable state storage перед multi-instance или restart-sensitive workflows;
- держи logs searchable по update id, chat id, handler и exception type.

## Webhook app

Webhook deployment - это ASP.NET Core app:

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

Не добавляй `AddTeleFlowHostedService()` в webhook apps. Webhooks управляются ASP.NET Core endpoint routing, а process lifetime уже принадлежит `WebApplication`.

Operational rules:

- expose HTTPS;
- держи webhook path стабильным;
- настраивай Telegram webhook URL вне request handler;
- валидируй `SecretToken`;
- держи request handling коротким;
- long-running work выноси за свою queue, когда это нужно.

## Configuration

Production tokens должны приходить из platform, а не из committed files:

```csharp
options.Token = configuration["Telegram:BotToken"]
    ?? throw new InvalidOperationException("Telegram:BotToken is not configured.");
```

Используй environment variables, secret managers, CI/CD variables, Docker secrets, Kubernetes secrets или secret storage своего cloud provider.

Смотри [Конфигурация и секреты](../getting-started/configuration.md).

## Docker

TeleFlow bot publish-ится как обычное .NET app:

```bash
dotnet publish -c Release -o publish
```

Для containers:

- передавай token через environment или secrets;
- не bake-ай secrets в image;
- отправляй logs в stdout/stderr;
- настрой graceful shutdown;
- не используй memory storage для workflows, которые должны переживать container restarts.

Long polling containers обычно должны работать в одном replica на bot token. Webhook containers можно масштабировать горизонтально, если handlers и storage готовы к concurrency.

## Lifecycle tasks в production

TeleFlow startup и shutdown tasks выполняются на каждом application instance:

```csharp
builder.Services.AddTeleFlowStartupTask<ConfigureBotCommands>();
builder.Services.AddTeleFlowShutdownTask<FlushMetrics>();
```

Хорошие примеры startup tasks:

- настроить Telegram bot commands;
- прогреть local caches;
- проверить обязательные external dependencies;
- подготовить process-local resources.

Плохие примеры startup tasks:

- non-idempotent database migrations;
- создание global resources без distributed lock;
- long-running background loops;
- работа, которая должна жить в deployment infrastructure.

В multi-replica deployments каждая replica запускает одни и те же startup tasks. Делай их idempotent или защищай infrastructure-level coordination. Shutdown tasks выполняются после остановки update source и должны укладываться в graceful shutdown window платформы.

## systemd

Для небольшого Linux host можно запустить long polling под `systemd`:

```ini
[Service]
WorkingDirectory=/opt/teleflow-bot
ExecStart=/usr/bin/dotnet /opt/teleflow-bot/MyBot.dll
Restart=on-failure
Environment=Telegram__BotToken=123456:ABC
```

По возможности держи secrets в environment file с ограниченными permissions.

## Kubernetes

Для Kubernetes:

- используй `Deployment` для webhook apps;
- используй один replica для long polling, если нет worker coordination;
- храни tokens в `Secret`;
- expose webhooks через ingress с TLS;
- используй readiness/liveness probes для ASP.NET Core webhook apps;
- используй external storage для state.

## Pending updates

Telegram может хранить updates, пока бот offline. При long polling pending updates могут прийти после restart.

Current public TeleFlow docs не заявляют `drop_pending_updates` option. Пока такого API нет, startup behavior нужно считать operational decision:

- handlers должны терпеть duplicate или delayed user actions;
- critical flows должны быть idempotent;
- deployments не должны создавать долгий downtime;
- если old updates вредны для продукта, explicit drop-pending support нужно запланировать до production launch.

## Release checklist

Перед production:

- используется generated registration или reflection use задокументирован;
- `IWF.TeleFlow.Generators` private;
- token и webhook secret не committed;
- transport choice documented;
- state storage подходит под deployment topology;
- logs и errors observable;
- CI запускает build и tests;
- smoke test покрывает `/start`, один callback, один state flow и один Telegram API call.
