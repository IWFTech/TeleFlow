# Handlers And Routing

Handlers are the main user-facing part of TeleFlow. A handler is a normal class with one or more methods decorated by routing attributes.

## Command Handler

```csharp
public sealed class StartHandler
{
    [Command("start")]
    public Task Handle(MessageContext ctx, CancellationToken ct)
    {
        return ctx.Message.AnswerAsync("Welcome.", ct);
    }
}
```

`[Command("start")]` matches `/start` by default. The command prefix can be changed through attribute properties.

Command prefix matching is explicit. The default is `CommandPrefixMode.Required`,
which means the user must send `/start` or another configured prefix:

```csharp
[Command("start", Prefixes = new[] { "/", "!" })]
public Task Start(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Welcome.", ct);
}
```

Use `CommandPrefixMode.Optional` when the same route should accept both a
Telegram command and prefix-less command-like text:

```csharp
[Command("help", PrefixMode = CommandPrefixMode.Optional)]
public Task Help(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Help.", ct);
}
```

Use `CommandPrefixMode.NoPrefix` when you want command routing semantics, but
only for prefix-less text:

```csharp
[Command("help", PrefixMode = CommandPrefixMode.NoPrefix)]
public Task HelpText(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Help.", ct);
}
```

## Text Handler

```csharp
public sealed class MenuHandler
{
    [Text("Settings")]
    public Task Settings(MessageContext ctx, CancellationToken ct)
    {
        return ctx.Message.AnswerAsync("Opening settings.", ct);
    }
}
```

`[Text]` supports match modes through `TextMatchMode`.

```csharp
[Text("support", TextMatchMode.Contains, ignoreCase: true)]
public Task ContainsSupport(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Support request detected.", ct);
}
```

## Template Routes

Templates are useful when the message has a stable structure:

```csharp
[CommandTemplate("ticket {id:long}")]
public Task ShowTicket(MessageContext ctx, long id, CancellationToken ct)
{
    return ctx.Message.AnswerAsync($"Ticket #{id}", ct);
}
```

The template describes the command body, not the prefix. Write
`[CommandTemplate("ticket {id:long}")]`, not
`[CommandTemplate("/ticket {id:long}")]`.

If you need to support both `/ticket 42` and `ticket 42`, keep one handler and
make the prefix optional:

```csharp
[CommandTemplate(
    "ticket {id:long}",
    PrefixMode = CommandPrefixMode.Optional)]
public Task ShowTicket(MessageContext ctx, long id, CancellationToken ct)
{
    return ctx.Message.AnswerAsync($"Ticket #{id}", ct);
}
```

This replaces the old workaround where the same method had both
`[CommandTemplate]` and `[TextTemplate]`.

Text templates work without a command prefix:

```csharp
[TextTemplate("order {id:long}")]
public Task ShowOrder(MessageContext ctx, long id, CancellationToken ct)
{
    return ctx.Message.AnswerAsync($"Order #{id}", ct);
}
```

## Regex Routes

Use regex routes when the input shape is not well represented by a template:

```csharp
[TextRegex(@"^INV-(\d+)$")]
public Task Invoice(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Invoice code received.", ct);
}
```

Regex routes are powerful, but templates are usually easier to read and maintain.

## Media Filters

TeleFlow includes marker attributes for common message content:

```csharp
public sealed class UploadHandler
{
    [HasPhoto]
    public Task Photo(MessageContext ctx, CancellationToken ct)
    {
        return ctx.Message.AnswerAsync("Photo received.", ct);
    }

    [HasDocument]
    public Task Document(MessageContext ctx, CancellationToken ct)
    {
        return ctx.Message.AnswerAsync("Document received.", ct);
    }
}
```

Other media filters include audio, voice, video, video note, animation, contact, dice, location, poll, sticker, venue, caption, and message thread markers.

Routes and filters both use C# attribute syntax. Route attributes such as `[CommandTemplate]` select the handler and bind values. Filter attributes such as `[HasPhoto]`, `[ChatType]`, and `[UseFilter<TFilter>]` add conditions before invocation. See [Filters](filters.md) for the full distinction.

## Class-Level Metadata

Routing and filter attributes can be placed on the class when every method should share the same condition:

```csharp
[ChatType(TelegramChatType.Private)]
public sealed class PrivateChatHandlers
{
    [Command("profile")]
    public Task Profile(MessageContext ctx, CancellationToken ct)
    {
        return ctx.Message.AnswerAsync("Private profile.", ct);
    }
}
```

## Handler Parameters

Handler methods can receive:

- the current context;
- route values from templates;
- typed callback payloads;
- `CancellationToken`;
- services from dependency injection.

```csharp
public sealed class TicketHandler
{
    [CommandTemplate("ticket {id:long}")]
    public Task Handle(
        MessageContext ctx,
        long id,
        ITicketRepository tickets,
        CancellationToken ct)
    {
        return ctx.Message.AnswerAsync($"Ticket #{id}", ct);
    }
}
```

Keep method signatures readable. If the handler needs many services, move application logic into a service and inject that service.

## Direct Registration

Direct registration is simple and explicit:

```csharp
builder.Services.AddTelegramHandler<StartHandler>();
builder.Services.AddTelegramModule<AdminHandlers>();
```

Use it for small apps, tests, or narrow modules. Direct registration registers
only the named handler or module type; it does not scan the assembly around it.

## Assembly Registration

Generated assembly registration is the recommended default for larger applications:

```csharp
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
```

This requires `IWF.TeleFlow.Generators`. Missing generated metadata is treated as a startup configuration error.
