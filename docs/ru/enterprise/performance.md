# Performance и scaling

TeleFlow спроектирован так, чтобы runtime behavior оставался предсказуемым, а не магическим. Performance work должен начинаться с измерений.

## Что обычно стоит времени

Для большинства Telegram bots framework редко является первым bottleneck. Перед оптимизацией framework internals измерь:

- Telegram Bot API latency;
- количество Telegram API requests на update;
- handler I/O;
- database или storage latency;
- external HTTP calls;
- serialization больших payloads;
- queue или webhook infrastructure latency.

## Что TeleFlow делает для понятного overhead

TeleFlow держит recommended runtime path явным:

- handler metadata генерируется at build time при `AddTelegramHandlersFromAssembly(...)`;
- missing generated metadata падает на startup, а не уходит в reflection fallback;
- handlers - обычные DI-created classes;
- Telegram Bot API calls остаются видимыми через `ITelegramClient`;
- middleware order задаётся явно через service registration;
- state storage - replaceable contract, а не hidden global state.

Это не означает, что любой бот автоматически быстрый. Это означает, что основные costs видимы и измеримы.

## Что измерять

Минимальный набор metrics:

| Metric | Зачем |
| --- | --- |
| Update processing duration | Находит slow handlers и slow middleware. |
| Handler duration | Разделяет framework path и business logic. |
| Telegram request count per update | Находит chatty handlers. |
| Telegram request latency | Telegram/network часто dominates response time. |
| Storage latency | State и wizard flows зависят от storage. |
| Error rate by handler | Находит unstable routes. |
| Queue delay или webhook request time | Нужно для queues и webhooks. |

## Handler guidance

Для handlers:

- передавай `CancellationToken` в I/O;
- избегай лишних Telegram API calls;
- batch work в application services, когда возможно;
- expensive work выноси из update critical path;
- используй queues для long-running jobs;
- держи callback payloads compact;
- держи state data small.

Плохой pattern:

```csharp
public async Task Handle(MessageContext ctx, CancellationToken ct)
{
    await service.CallAAsync(ct);
    await service.CallBAsync(ct);
    await service.CallCAsync(ct);
    await ctx.Message.AnswerAsync("Done.", ct);
}
```

Лучше, если работа дорогая:

```csharp
public async Task Handle(MessageContext ctx, IJobQueue queue, CancellationToken ct)
{
    await queue.EnqueueAsync(new BuildReportJob(ctx.TelegramChat.Id), ct);
    await ctx.Message.AnswerAsync("Report queued.", ct);
}
```

## Long polling scaling

Long polling обычно должен работать как один active worker на bot token. Несколько uncoordinated long polling workers на один token создают operational ambiguity вокруг update ownership.

Если нужен больший throughput:

- держи handlers быстрыми;
- выноси долгую работу в queue;
- делай state storage durable;
- делай handlers idempotent;
- рассмотри Telegram gateway, который пишет raw updates в broker, а дальше updates обрабатываются твоей worker model.

Raw transports существуют именно для такой gateway architecture.

## Webhook scaling

Webhook apps можно масштабировать horizontally, если application state готов к concurrency.

Requirements:

- external state storage;
- idempotent handlers для critical actions;
- shared callback/state semantics между instances;
- short request processing time;
- retry-aware behavior, потому что Telegram может retry-ить webhook delivery.

Используй queue, если webhook processing может превышать нормальный HTTP request budget.

## Storage scaling

Memory storage process-local. Это не scaling strategy.

Для production storage:

- определи key structure;
- определи TTL и cleanup;
- определи concurrency behavior;
- измеряй latency;
- тестируй process restart;
- тестируй wizard back/reset behavior;
- отдавай operational metrics из storage implementation.

## Benchmarking

Полезные benchmark groups:

- handler selection and invocation;
- generated registration startup;
- callback serialization/deserialization;
- state get/set/reset с выбранным storage;
- long polling update processing с mocked Telegram client;
- webhook request-to-handler latency;
- end-to-end scenario с real database и mocked Telegram API.

Не оптимизируй на догадках. Первый benchmark должен быть simple и repeatable.

Репозиторный benchmark suite находится в [benchmarks/README.md](https://github.com/IWFTech/TeleFlow/blob/main/benchmarks/README.md). Сейчас он включает честный набор `Scenarios/Vs`: TeleFlow vs Telegram.Bot low-level client calls, TeleFlow raw long polling vs handwritten Telegram.Bot polling loop, TeleFlow vs Telegram.Bot update deserialization и TeleFlow vs Telegrator command/callback framework dispatch. Adapter для конкурента добавляется только тогда, когда можно доказать, что benchmark реально исполнил измеряемый path без network I/O.
