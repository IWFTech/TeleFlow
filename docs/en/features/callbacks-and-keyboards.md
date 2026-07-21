# Callbacks And Keyboards

Telegram bots often become real applications when inline buttons appear. TeleFlow supports raw callback data, typed callback payloads, callback helpers, and keyboard builders.

## Inline Keyboard With Raw Callback Data

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

## Typed Callback Payloads

Typed payloads are better when callback data carries structured information:

```csharp
[CallbackData("ticket")]
public sealed record TicketAction(long Id, string Action);
```

Button style examples below use generated Bot API constants from
`TeleFlow.Telegram.Schema.Constants`.

Create buttons:

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

Telegram callback data is limited to 64 UTF-8 bytes. Keep payloads compact.

Typed callback buttons use `[CallbackData]` compact metadata directly.
Raw callback strings are preserved exactly and do not go through typed payload packing.

`[CallbackData("ticket")]` enables TeleFlow's compact callback data format for that payload type. A payload like `new TicketAction(42, "take")` is packed as a short prefix-plus-fields string instead of verbose JSON. If the packed value exceeds Telegram's 64-byte limit, TeleFlow fails before sending the keyboard.

When `IWF.TeleFlow.Generators` is installed, TeleFlow emits callback data codecs at build time. The generated path packs typed keyboard payloads, matches incoming callback data, and constructs typed payloads without `PropertyInfo.GetValue(...)`, `ConstructorInfo.Invoke(...)`, or `Activator.CreateInstance(...)` on every callback. Direct registration and deprecated reflection registration can still use the runtime metadata fallback.

Typed inline keyboard payloads must be marked with `[CallbackData]`. If you need an external key, Redis key, or your own opaque format, pass raw string callback data instead.

Custom `ICallbackDataSerializer` implementations remain available for advanced callback payload serialization and route deserialization, but typed inline keyboard buttons use `[CallbackData]` metadata directly.

Invalid compact typed callback data does not invoke the typed handler. If the
callback data matches the typed route prefix and field shape but cannot be
decoded, TeleFlow logs a warning from `TelegramHandlerSelector` and treats that
typed route as not matched. This covers stale buttons from an older bot version,
malformed numeric fields, invalid boolean values, and invalid enum values.

If you want a graceful user-facing answer for old buttons, add a raw callback
fallback after the typed handler:

```csharp
[Callback<TicketAction>]
public Task TicketActionCallback(
    CallbackQueryContext ctx,
    TicketAction payload,
    CancellationToken ct)
{
    // Normal typed path.
    return Task.CompletedTask;
}

[Callback]
[CallbackDataPrefix("ticket")]
public async Task StaleTicketCallback(
    CallbackQueryContext ctx,
    CancellationToken ct)
{
    await ctx.Callback.AnswerAsync(
        "This button is outdated. Open the ticket again.",
        showAlert: true,
        cancellationToken: ct);
}
```

The warning intentionally does not include the raw callback data string, because
callback data can contain internal keys. Use compact prefixes and short fields
instead of placing large or sensitive data directly into Telegram callback data.

JSON fallback callback payloads remain supported, but TeleFlow cannot reliably
distinguish "not my callback" from "my stale JSON payload" without a compact
prefix. Use `[CallbackData]` for production callback routes.

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

## Native Telegram Markup

`InlineKeyboardBuilder` is only a convenience layer. TeleFlow helpers accept
the native generated `InlineKeyboardMarkup`, so new Telegram keyboard fields
can be used directly as soon as the schema package contains them:

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

Use the builder for common cases. Use native markup when you need full Bot API
control or a newly added Telegram field that the builder does not wrap yet.
If Telegram exposes a string value before the schema package has generated a
constant for it, pass that raw string directly.

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

## Ephemeral Callback Responses

In a group or supergroup, a callback handler can send a response visible only to the user who pressed the button:

```csharp
[Callback]
public async Task ShowPrivateDetails(CallbackQueryContext ctx, CancellationToken ct)
{
    await ctx.Callback.SendEphemeralAsync("Only you can see these details.", ct);
    await ctx.Callback.AnswerAsync(ct);
}
```

`SendEphemeralAsync(...)` passes the callback query ID to Telegram, but it does **not** answer the callback query. Keep `AnswerAsync(...)` or the auto-answer middleware so Telegram stops showing the loading indicator.

If the button belongs to an ephemeral message, `EditTextAsync(...)` and `DeleteMessageAsync(...)` select Telegram's dedicated ephemeral endpoints automatically. The response is still best-effort: Telegram can fail to deliver it, and an ephemeral message reference is not durable state.
