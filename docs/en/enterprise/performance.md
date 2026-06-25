# Performance And Scaling

TeleFlow is designed to keep runtime behavior predictable, not magical. Performance work should be based on measurement.

## What Usually Costs Time

For most Telegram bots, the framework is rarely the first bottleneck. Measure these before optimizing framework internals:

- Telegram Bot API latency;
- number of Telegram API requests per update;
- handler I/O;
- database or storage latency;
- external HTTP calls;
- serialization of large payloads;
- queue or webhook infrastructure latency.

## What TeleFlow Does To Keep Overhead Clear

TeleFlow keeps the recommended runtime path explicit:

- handler metadata is generated at build time when using `AddTelegramHandlersFromAssembly(...)`;
- missing generated metadata fails at startup instead of falling back to reflection;
- handlers are normal DI-created classes;
- Telegram Bot API calls remain visible through `ITelegramClient`;
- middleware order is explicit through service registration;
- state storage is a replaceable contract, not hidden global state.

This does not mean every bot is automatically fast. It means the major costs are visible and measurable.

## Metrics To Track

Track at least:

| Metric | Why |
| --- | --- |
| Update processing duration | Finds slow handlers and slow middleware. |
| Handler duration | Separates framework path from business logic. |
| Telegram request count per update | Finds chatty handlers. |
| Telegram request latency | Telegram/network often dominates response time. |
| Storage latency | State and wizard flows depend on storage. |
| Error rate by handler | Finds unstable routes. |
| Queue delay or webhook request time | Required when using queues or webhooks. |

## Handler Guidance

For handlers:

- pass `CancellationToken` to I/O;
- avoid unnecessary Telegram API calls;
- batch work in application services when possible;
- keep expensive work outside the update critical path;
- use queues for long-running jobs;
- keep callback payloads compact;
- keep state data small.

Bad pattern:

```csharp
public async Task Handle(MessageContext ctx, CancellationToken ct)
{
    await service.CallAAsync(ct);
    await service.CallBAsync(ct);
    await service.CallCAsync(ct);
    await ctx.Message.AnswerAsync("Done.", ct);
}
```

Better when the work is expensive:

```csharp
public async Task Handle(MessageContext ctx, IJobQueue queue, CancellationToken ct)
{
    await queue.EnqueueAsync(new BuildReportJob(ctx.TelegramChat.Id), ct);
    await ctx.Message.AnswerAsync("Report queued.", ct);
}
```

## Long Polling Scaling

Long polling should normally run as one active worker per bot token. Running multiple uncoordinated long polling workers for the same token can create operational ambiguity around update ownership.

If you need more throughput:

- keep handlers fast;
- move long work into a queue;
- make state storage durable;
- make handlers idempotent;
- consider a Telegram gateway that writes raw updates into a broker, then process updates with your own worker model.

Raw transports exist for that gateway architecture.

## Webhook Scaling

Webhook apps can scale horizontally when application state is safe for concurrency.

Requirements:

- external state storage;
- idempotent handlers for critical actions;
- shared callback/state semantics across instances;
- short request processing time;
- retry-aware behavior because Telegram can retry webhook delivery.

Use a queue when webhook processing can exceed normal HTTP request budgets.

## Storage Scaling

Memory storage is process-local. It is not a scaling strategy.

For production storage:

- define key structure;
- define TTL and cleanup;
- define concurrency behavior;
- measure latency;
- test process restart;
- test wizard back/reset behavior;
- expose operational metrics from your storage implementation.

## Benchmarking

Useful benchmark groups:

- handler selection and invocation;
- generated registration startup;
- callback serialization/deserialization;
- state get/set/reset with chosen storage;
- long polling update processing with mocked Telegram client;
- webhook request-to-handler latency;
- end-to-end scenario with real database and mocked Telegram API.

Do not optimize by guessing. Keep the first benchmark simple and repeatable.

The repository benchmark suite lives in [benchmarks/README.md](../../../benchmarks/README.md). It currently includes a fair `Scenarios/Vs` comparison set for TeleFlow vs Telegram.Bot low-level client calls, TeleFlow raw long polling vs a handwritten Telegram.Bot polling loop, TeleFlow vs Telegram.Bot update deserialization, and TeleFlow vs Telegrator command/callback framework dispatch. Competitor adapters are only accepted when they can prove that the benchmarked path actually executed without network I/O.
