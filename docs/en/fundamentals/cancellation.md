# Cancellation

TeleFlow carries cancellation through the update pipeline. Handler methods can receive the current update token, and context-bound Telegram helpers use that token when you do not pass one explicitly.

## Handler Tokens

```csharp
[Command("start")]
public Task Start(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Ready.");
}
```

Keep the token in the handler signature even if a small handler does not use it yet. The handler is the boundary where application services, repositories, HTTP clients, queues, and other I/O should receive cancellation explicitly.

```csharp
[Command("report")]
public async Task Report(MessageContext ctx, IReportService reports, CancellationToken ct)
{
    var report = await reports.BuildAsync(ctx.Sender?.Id, ct);
    await ctx.Message.AnswerAsync(report.Title);
}
```

## Context-Bound Telegram Helpers

The helpers exposed by TeleFlow contexts are bound to the current update:

```csharp
await ctx.Message.AnswerAsync("Saved.");
await ctx.Callback.AnswerAsync();
await ctx.Callback.EditTextAsync("Done.");
await ctx.Chat.ActionAsync(ChatAction.Typing);
```

If no token is passed, these helpers use the update cancellation token. A non-cancelable token such as `CancellationToken.None` is treated the same way in context-bound Telegram helpers. If you pass a cancelable token explicitly, that token wins.

## `ctx.Bot` Calls

Generated client methods also accept cancellation tokens, but `ctx.Bot` is update-scoped inside handlers:

```csharp
using TeleFlow.Telegram.Schema.Abstractions;

await ctx.Bot.SendMessageAsync(
    chatId: IntegerString.From(ctx.TelegramChat.Id),
    text: "Processing complete.");
```

The same generated method called on a root `ITelegramClient` from DI is not update-scoped. Outside a handler context, pass the token explicitly.

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

## Recommended Rule

Use this rule:

- keep `CancellationToken ct` in async handlers;
- pass `ct` to your own I/O: database calls, HTTP calls, queues, storage, EF Core, and background work;
- omit `ct` for `ctx.Message`, `ctx.Callback`, `ctx.Chat`, and `ctx.Bot` calls inside handlers when you want the current update token;
- pass an explicit token only when you intentionally want different cancellation semantics;
- do not hide cancellation in application services through ambient state or static accessors.

This keeps small bots readable without making production code depend on hidden cancellation behavior.
