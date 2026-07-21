# Callbacks и keyboards

Telegram-бот часто становится настоящим приложением в момент, когда появляются inline buttons. TeleFlow поддерживает raw callback data, typed callback payloads, callback helpers и keyboard builders.

## Inline keyboard с raw callback data

```csharp
[Command("menu")]
public Task Menu(MessageContext ctx, CancellationToken ct)
{
    var keyboard = InlineKeyboardBuilder.Create()
        .Button("Profile", "menu:profile")
        .Button("Settings", "menu:settings")
        .Row()
        .Url("Docs", "https://example.com/docs")
        .Build();

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

Примеры button style ниже используют generated Bot API constants из
`TeleFlow.Telegram.Schema.Constants`.

Создание buttons:

```csharp
[CommandTemplate("ticket {id:long}")]
public Task Ticket(
    MessageContext ctx,
    long id,
    CancellationToken ct)
{
    var keyboard = InlineKeyboardBuilder.Create()
        .Button(
            "Take",
            new TicketAction(id, "take"),
            new InlineKeyboardButtonOptions { Style = ButtonStyles.Primary })
        .Button(
            "Resolve",
            new TicketAction(id, "resolve"),
            new InlineKeyboardButtonOptions { Style = ButtonStyles.Success })
        .Build();

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

Typed callback buttons используют compact metadata из `[CallbackData]` напрямую.
Raw callback strings сохраняются как есть и не проходят через typed payload packing.

`[CallbackData("ticket")]` включает compact callback data format для этого payload type. Payload вроде `new TicketAction(42, "take")` упаковывается в короткую строку prefix-plus-fields вместо verbose JSON. Если packed value превышает Telegram limit в 64 bytes, TeleFlow падает до отправки keyboard.

Если установлен `IWF.TeleFlow.Generators`, TeleFlow генерирует callback data codecs на этапе сборки. Generated path пакует typed keyboard payloads, матчится с входящей callback data и создаёт typed payloads без `PropertyInfo.GetValue(...)`, `ConstructorInfo.Invoke(...)` и `Activator.CreateInstance(...)` на каждом callback. Direct registration и deprecated reflection registration всё ещё могут использовать runtime metadata fallback.

Typed inline keyboard payloads должны быть помечены `[CallbackData]`. Если нужен внешний ключ, Redis key или свой opaque format, передавай raw string callback data.

Custom `ICallbackDataSerializer` implementations остаются доступны для advanced callback payload serialization и route deserialization, но typed inline keyboard buttons используют `[CallbackData]` metadata напрямую.

Invalid compact typed callback data не вызывает typed handler. Если callback
data совпала с prefix и field shape typed route, но не смогла распаковаться,
TeleFlow пишет warning от `TelegramHandlerSelector` и считает этот typed route
не сматченным. Это покрывает старые кнопки от предыдущей версии бота,
сломанные numeric fields, invalid boolean values и invalid enum values.

Если нужен красивый ответ пользователю для старых кнопок, добавь raw callback
fallback после typed handler:

```csharp
[Callback<TicketAction>]
public Task TicketActionCallback(
    CallbackQueryContext ctx,
    TicketAction payload,
    CancellationToken ct)
{
    // Нормальный typed path.
    return Task.CompletedTask;
}

[Callback]
[CallbackDataPrefix("ticket")]
public async Task StaleTicketCallback(
    CallbackQueryContext ctx,
    CancellationToken ct)
{
    await ctx.Callback.AnswerAsync(
        "Эта кнопка устарела. Открой заявку заново.",
        showAlert: true,
        cancellationToken: ct);
}
```

Warning намеренно не содержит raw callback data string, потому что callback data
может содержать internal keys. Используй compact prefixes и короткие fields,
а large или sensitive data не клади напрямую в Telegram callback data.

JSON fallback callback payloads остаются поддержанными, но без compact prefix
TeleFlow не может надёжно отличить "это не мой callback" от "это мой старый JSON
payload". Для production callback routes используй `[CallbackData]`.

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

## Нативная Telegram markup

`InlineKeyboardBuilder` - это только удобный слой. Helpers в TeleFlow принимают
нативный generated `InlineKeyboardMarkup`, поэтому новые поля Telegram keyboard
можно использовать напрямую, как только они появились в schema package:

```csharp
var keyboard = new InlineKeyboardMarkup
{
    InlineKeyboard =
    [
        [
            new InlineKeyboardButton
            {
                Text = "Delete",
                CallbackData = "ticket:delete:42",
                Style = ButtonStyles.Danger
            }
        ]
    ]
};

await ctx.Message.AnswerAsync("Choose:", keyboard, ct);
```

Builder стоит использовать для обычных сценариев. Нативная markup нужна, когда
требуется полный контроль Bot API или новое Telegram-поле, для которого builder
ещё не успел получить удобный wrapper. Если Telegram добавил string value до
того, как schema package сгенерировал для него constant, передай эту строку
напрямую.

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

## Ephemeral-ответы на callback

В группе или супергруппе callback handler может отправить ответ, видимый только нажавшему кнопку пользователю:

```csharp
[Callback]
public async Task ShowPrivateDetails(CallbackQueryContext ctx, CancellationToken ct)
{
    await ctx.Callback.SendEphemeralAsync("Эти детали видите только вы.", ct);
    await ctx.Callback.AnswerAsync(ct);
}
```

`SendEphemeralAsync(...)` передаёт Telegram ID callback query, но **не** отвечает на сам callback. Оставь `AnswerAsync(...)` или auto-answer middleware, чтобы Telegram убрал индикатор загрузки.

Если кнопка находится в ephemeral message, `EditTextAsync(...)` и `DeleteMessageAsync(...)` сами выберут специальные Telegram endpoints для ephemeral messages. Но доставка всё равно best-effort: Telegram может не доставить ответ, а ссылка на ephemeral message не является постоянным состоянием.
