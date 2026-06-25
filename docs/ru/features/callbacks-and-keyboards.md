# Callbacks и keyboards

Telegram-бот часто становится настоящим приложением в момент, когда появляются inline buttons. TeleFlow поддерживает raw callback data, typed callback payloads, callback helpers и keyboard builders.

## Inline keyboard с raw callback data

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

## Typed callback payloads

Typed payloads лучше, когда callback data несёт structured information:

```csharp
[CallbackData("ticket")]
public sealed record TicketAction(long Id, string Action);
```

Создание buttons:

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

Telegram callback data ограничен 64 UTF-8 bytes. Payloads должны быть компактными.

`[CallbackData("ticket")]` включает compact serializer для этого payload type. Payload вроде `new TicketAction(42, "take")` сериализуется в короткую строку prefix-plus-fields вместо verbose JSON. Если serialized value превышает Telegram limit в 64 bytes, TeleFlow падает до отправки keyboard.

Invalid typed callback data не матчится с typed handler. Serializer failures, которые не являются обычными parse/format failures, считаются реальными failures и остаются observable.

## Auto answer callback

Callbacks обычно нужно отвечать. Можно вручную:

```csharp
await ctx.Callback.AnswerAsync(ct);
```

Или глобально:

```csharp
builder.Services.AddAutoCallbackAnswer(options =>
{
    options.Text = "Done";
});
```

Можно пометить конкретный callback handler:

```csharp
[Callback<TicketAction>]
[AutoAnswerCallback("Saved")]
public Task Handle(CallbackQueryContext ctx, TicketAction payload)
{
    return Task.CompletedTask;
}
```

Глобальный default можно отключить для одного handler:

```csharp
[Callback<TicketAction>]
[AutoAnswerCallback(Enabled = false)]
public Task HandleWithoutAutoAnswer(CallbackQueryContext ctx, TicketAction payload)
{
    return Task.CompletedTask;
}
```

Auto-answer выполняется только после успешного callback handler. Он не отправляет второй answer, если handler уже вызвал `ctx.Callback.AnswerAsync(...)`.

Manual answers лучше, когда callback feedback зависит от application logic.

## Reply keyboard

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

## Remove keyboard

```csharp
await ctx.Message.AnswerAsync(
    "Keyboard removed.",
    KeyboardRemove.Create(),
    ct);
```

## Force reply

```csharp
await ctx.Message.AnswerAsync(
    "Describe the issue.",
    ForceReplyBuilder.Create().Placeholder("Short description"),
    ct);
```

## Callback helpers

`CallbackQueryContext.Callback` включает:

- `AnswerAsync(...)`;
- `EditTextAsync(...)`;
- `DeleteMessageAsync(...)`.

Для Bot API методов, которых нет в helpers, используй `ctx.Bot`.
