# Enterprise guide

Enterprise adoption - это не больше абстракций. Это предсказуемое поведение, понятное владение, testability и operational failure modes.

TeleFlow может быть приемлемым для серьёзных .NET services, потому что важные вещи остаются явными:

- transport выбирается явно;
- handler registration mode виден в коде;
- generated registration падает, если metadata нет;
- Telegram client остаётся напрямую доступным;
- core state и storage contracts replaceable;
- framework internals используют обычный DI и logging.

## Рекомендуемые enterprise defaults

Используй:

```csharp
builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
```

И ровно один transport:

```csharp
builder.Services.AddLongPolling();
```

Или:

```csharp
builder.Services.AddWebhook(options => options.Path = "/telegram/webhook");
```

Generated registration - default. Reflection registration только если это documented intentional decision.

## Границы

Telegram-specific code держи рядом с bot layer:

```text
Bot
  Handlers
  Filters
  Scenes
  Telegram-specific DTO mapping
Application
  Use cases
  Services
Domain
  Business models
Infrastructure
  Storage
  External clients
```

Не протаскивай Telegram DTOs везде, если только весь продукт не Telegram-only и это осознанный tradeoff.

## State policy

Memory state storage - не enterprise storage strategy. Это development и single-process option.

Для production:

- выбери durable storage;
- определи key structure;
- реши concurrency semantics;
- реши TTL и cleanup;
- протестируй wizard history;
- протестируй recovery после process restart.

## Error policy

Error handlers используй для known business failures. Unknown failures должны оставаться visible.

Рекомендуемо:

- handler-level error handlers для validation и expected user errors;
- structured logs для unknown exceptions;
- host-level restart policy;
- alerting на repeated failures;
- tests для handled error flows.

## Performance policy

Не считай framework bottleneck без измерений. Измеряй:

- handler time;
- Telegram API request count;
- Telegram API request latency;
- storage latency;
- queue или webhook latency, если есть.

TeleFlow уже снимает главное enterprise-возражение: recommended assembly registration path не опирается на silent runtime reflection.

Смотри [Performance и scaling](performance.md) для guidance по измерениям и scaling.

## Upgrade policy

Держи package updates controlled:

- pin package versions;
- запускай tests на dependency updates;
- тестируй generated registration после generator updates;
- отслеживай Telegram Bot API schema updates отдельно от application behavior;
- веди release notes.

Смотри [Версионирование и релизы](versioning.md).

## Что документировать в своём проекте

Каждый production bot должен документировать:

- transport choice;
- registration mode;
- storage backend;
- state key policy;
- callback payload policy;
- error handling policy;
- deployment topology;
- как replay or inspect failed updates;
- кто владеет bot token rotation.
