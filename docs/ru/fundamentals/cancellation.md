# Cancellation

TeleFlow передаёт cancellation через пайплайн обновлений. Хэндлер может принимать token текущего обновления, а Telegram-хелперы, привязанные к context, используют этот token автоматически, если ты не передал другой token явно.

## Токены в хэндлерах

```csharp
[Command("start")]
public Task Start(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Ready.");
}
```

Оставляй `CancellationToken` в async хэндлере, даже если маленький хэндлер пока его не использует. Хэндлер - это граница, где прикладные сервисы, репозитории, HTTP-клиенты, очереди и другая I/O-логика должны получать cancellation явно.

```csharp
[Command("report")]
public async Task Report(MessageContext ctx, IReportService reports, CancellationToken ct)
{
    var report = await reports.BuildAsync(ctx.Sender?.Id, ct);
    await ctx.Message.AnswerAsync(report.Title);
}
```

## Telegram-хелперы из context

Хелперы, которые TeleFlow отдаёт через context, привязаны к текущему обновлению:

```csharp
await ctx.Message.AnswerAsync("Saved.");
await ctx.Callback.AnswerAsync();
await ctx.Callback.EditTextAsync("Done.");
await ctx.Chat.ActionAsync(ChatAction.Typing);
```

Если token не передан, эти helpers используют cancellation token текущего обновления. Token, который нельзя отменить, например `CancellationToken.None`, в этих Telegram-хелперах трактуется так же. Если ты передал cancelable token явно, он переопределит token обновления.

## Вызовы через `ctx.Bot`

Сгенерированные методы клиента тоже принимают cancellation tokens, но `ctx.Bot` внутри хэндлера является update-scoped:

```csharp
using TeleFlow.Telegram.Schema.Abstractions;

await ctx.Bot.SendMessageAsync(
    chatId: IntegerString.From(ctx.TelegramChat.Id),
    text: "Processing complete.");
```

Тот же generated method, вызванный на root `ITelegramClient` из DI, уже не привязан к обновлению. Вне context хэндлера передавай token явно.

```csharp
public sealed class BroadcastService(ITelegramClient bot)
{
    public Task SendAsync(long chatId, string text, CancellationToken ct)
    {
        return bot.SendMessageAsync(
            chatId: IntegerString.From(chatId),
            text: text,
            cancellationToken: ct);
    }
}
```

## Рекомендуемое правило

Держи правило таким:

- оставляй `CancellationToken ct` в async хэндлерах;
- передавай `ct` в свою I/O-логику: database calls, HTTP calls, очереди, storage, EF Core и background work;
- не передавай `ct` в `ctx.Message`, `ctx.Callback`, `ctx.Chat` и `ctx.Bot` внутри хэндлера, если тебе нужен token текущего обновления;
- передавай явный token только если тебе намеренно нужны другие cancellation semantics;
- не прячь cancellation в application services через ambient state или static accessors.

Так маленький бот остаётся читаемым, а production code не начинает зависеть от скрытой магии.
