# Callbacks And Keyboards

Telegram bots often become real applications when inline buttons appear. TeleFlow supports raw callback data, typed callback payloads, callback helpers, and keyboard builders.

## Inline Keyboard With Raw Callback Data

```csharp
[Command("menu")]
public Task Menu(MessageContext ctx, CancellationToken ct)
{
    var keyboard = InlineKeyboard.Create()
        .Button("Profile", "menu:profile")
        .Button("Settings", "menu:settings")
        .Row()
        .Url("Docs", "https://example.com/docs");

    return ctx.Message.AnswerAsync("Choose:", keyboard, ct);
}
```

Handle raw callbacks:

```csharp
[Callback]
[CallbackDataPrefix("menu:")]
public async Task MenuCallback(CallbackQueryContext ctx, CancellationToken ct)
{
    await ctx.Callback.AnswerAsync(ct);
    await ctx.Callback.EditTextAsync($"Selected: {ctx.TelegramCallbackQuery.Data}", ct);
}
```

## Typed Callback Payloads

Typed payloads are better when callback data carries structured information:

```csharp
[CallbackData("ticket")]
public sealed record TicketAction(long Id, string Action);
```

Create buttons:

```csharp
[CommandTemplate("ticket {id:long}")]
public Task Ticket(MessageContext ctx, long id, CancellationToken ct)
{
    var keyboard = InlineKeyboard.Create()
        .Button("Take", new TicketAction(id, "take"))
        .Button("Resolve", new TicketAction(id, "resolve"));

    return ctx.Message.AnswerAsync($"Ticket #{id}", keyboard, ct);
}
```

Handle typed callbacks:

```csharp
[Callback<TicketAction>]
public async Task Handle(
    CallbackQueryContext ctx,
    TicketAction payload,
    ITicketService tickets,
    CancellationToken ct)
{
    await tickets.ApplyAsync(payload.Id, payload.Action, ct);
    await ctx.Callback.AnswerAsync("Saved", ct);
    await ctx.Callback.EditTextAsync($"Ticket {payload.Id}: {payload.Action}", ct);
}
```

Telegram callback data is limited to 64 UTF-8 bytes. Keep payloads compact.

`[CallbackData("ticket")]` enables TeleFlow's compact serializer for that payload type. A payload like `new TicketAction(42, "take")` is serialized as a short prefix-plus-fields string instead of verbose JSON. If the serialized value exceeds Telegram's 64-byte limit, TeleFlow fails before sending the keyboard.

Invalid typed callback data does not match the typed handler. Serializer failures that are not normal parse/format failures are treated as real failures and remain observable.

## Auto Answer Callback

Callbacks should usually be answered. You can answer manually:

```csharp
await ctx.Callback.AnswerAsync(ct);
```

Or configure automatic answers globally:

```csharp
builder.Services.AddAutoCallbackAnswer(options =>
{
    options.Text = "Done";
});
```

You can also mark a callback handler:

```csharp
[Callback<TicketAction>]
[AutoAnswerCallback("Saved")]
public Task Handle(CallbackQueryContext ctx, TicketAction payload)
{
    return Task.CompletedTask;
}
```

Disable a global default for one handler when needed:

```csharp
[Callback<TicketAction>]
[AutoAnswerCallback(Enabled = false)]
public Task HandleWithoutAutoAnswer(CallbackQueryContext ctx, TicketAction payload)
{
    return Task.CompletedTask;
}
```

Auto-answer runs only after a successful callback handler. It does not send a second answer when the handler already called `ctx.Callback.AnswerAsync(...)`.

Use explicit manual answers when callback feedback depends on application logic.

## Reply Keyboard

```csharp
var keyboard = ReplyKeyboard.Create()
    .Button("Create ticket")
    .Button("My tickets")
    .Row()
    .Button("Help")
    .Resize()
    .OneTime();

await ctx.Message.AnswerAsync("Choose an action.", keyboard, ct);
```

## Remove Keyboard

```csharp
await ctx.Message.AnswerAsync(
    "Keyboard removed.",
    KeyboardRemove.Create(),
    ct);
```

## Force Reply

```csharp
await ctx.Message.AnswerAsync(
    "Describe the issue.",
    ForceReplyBuilder.Create().Placeholder("Short description"),
    ct);
```

## Callback Helpers

`CallbackQueryContext.Callback` includes:

- `AnswerAsync(...)`;
- `EditTextAsync(...)`;
- `DeleteMessageAsync(...)`.

Use `ctx.Bot` for Bot API methods not covered by helpers.
