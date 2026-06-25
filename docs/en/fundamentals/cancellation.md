# Cancellation

TeleFlow passes cancellation through the update pipeline. Handler methods can receive `CancellationToken`, and context helpers use the update cancellation token when you do not pass one explicitly.

## Handler Tokens

```csharp
[Command("start")]
public Task Start(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Ready.", ct);
}
```

Use the token for I/O:

```csharp
[Command("report")]
public async Task Report(MessageContext ctx, IReportService reports, CancellationToken ct)
{
    var report = await reports.BuildAsync(ctx.Sender?.Id, ct);
    await ctx.Message.AnswerAsync(report.Title, ct);
}
```

## Context Helpers

Message and callback helpers accept optional cancellation tokens:

```csharp
await ctx.Message.AnswerAsync("Saved.", ct);
await ctx.Callback.AnswerAsync(ct);
await ctx.Callback.EditTextAsync("Done.", ct);
```

If no token is passed, helpers resolve the token from the current update context.

## Direct Bot API Calls

Generated client methods also accept cancellation tokens:

```csharp
using TeleFlow.Telegram.Schema.Abstractions;

await ctx.Bot.SendMessageAsync(
    chatId: IntegerString.From(ctx.TelegramChat.Id),
    text: "Processing complete.",
    cancellationToken: ct);
```

## Recommended Rule

Pass `CancellationToken` to:

- database calls;
- HTTP calls;
- Telegram Bot API calls;
- storage calls;
- long-running CPU or background work that supports cancellation.

It is acceptable to omit it for tiny pure in-memory operations, but it is usually clearer to keep handler I/O consistently cancellable.
