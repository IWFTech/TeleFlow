# Cancellation

TeleFlow передаёт cancellation через update pipeline. Handler methods могут принимать `CancellationToken`, а context helpers используют token текущего update, если ты не передал token явно.

## Handler tokens

```csharp
[Command("start")]
public Task Start(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Ready.", ct);
}
```

Используй token для I/O:

```csharp
[Command("report")]
public async Task Report(MessageContext ctx, IReportService reports, CancellationToken ct)
{
    var report = await reports.BuildAsync(ctx.Sender?.Id, ct);
    await ctx.Message.AnswerAsync(report.Title, ct);
}
```

## Context helpers

Message и callback helpers принимают optional cancellation tokens:

```csharp
await ctx.Message.AnswerAsync("Saved.", ct);
await ctx.Callback.AnswerAsync(ct);
await ctx.Callback.EditTextAsync("Done.", ct);
```

Если token не передан, helpers берут token из current update context.

## Прямые вызовы Bot API

Generated client methods тоже принимают cancellation tokens:

```csharp
using TeleFlow.Telegram.Schema.Abstractions;

await ctx.Bot.SendMessageAsync(
    chatId: IntegerString.From(ctx.TelegramChat.Id),
    text: "Processing complete.",
    cancellationToken: ct);
```

## Рекомендуемое правило

Передавай `CancellationToken` в:

- database calls;
- HTTP calls;
- Telegram Bot API calls;
- storage calls;
- долгие CPU/background operations, если они поддерживают cancellation.

Для маленьких pure in-memory operations token можно не передавать, но обычно понятнее держать handler I/O consistently cancellable.
