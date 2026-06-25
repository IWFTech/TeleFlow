# Хэндлеры и routing

Handlers - главная user-facing часть TeleFlow. Handler - обычный class с одним или несколькими methods, помеченными routing attributes.

## Command handler

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

`[Command("start")]` по умолчанию матчится на `/start`. Command prefix можно менять через свойства атрибута.

## Text handler

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

`[Text]` поддерживает match modes через `TextMatchMode`.

```csharp
[Text("support", TextMatchMode.Contains, ignoreCase: true)]
public Task ContainsSupport(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Support request detected.", ct);
}
```

## Template routes

Templates полезны, когда message имеет стабильную структуру:

```csharp
[CommandTemplate("ticket {id:long}")]
public Task ShowTicket(MessageContext ctx, long id, CancellationToken ct)
{
    return ctx.Message.AnswerAsync($"Ticket #{id}", ct);
}
```

Text templates работают без command prefix:

```csharp
[TextTemplate("order {id:long}")]
public Task ShowOrder(MessageContext ctx, long id, CancellationToken ct)
{
    return ctx.Message.AnswerAsync($"Order #{id}", ct);
}
```

## Regex routes

Regex routes нужны, когда input shape плохо выражается через template:

```csharp
[TextRegex(@"^INV-(\d+)$")]
public Task Invoice(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Invoice code received.", ct);
}
```

Regex powerful, но templates обычно проще читать и сопровождать.

## Media filters

TeleFlow содержит marker attributes для common message content:

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

Другие media filters: audio, voice, video, video note, animation, contact, dice, location, poll, sticker, venue, caption и message thread markers.

## Class-level metadata

Routing и filter attributes можно ставить на class, если все methods должны делить одно условие:

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

## Параметры handler

Handler methods могут получать:

- current context;
- route values из templates;
- typed callback payloads;
- `CancellationToken`;
- services из dependency injection.

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

Держи method signatures читаемыми. Если handler требует много services, вынеси application logic в service и inject этот service.

## Прямая регистрация

Direct registration простая и явная:

```csharp
builder.Services.AddTelegramHandler<StartHandler>();
builder.Services.AddTelegramModule<AdminHandlers>();
```

Используй её для маленьких apps, tests или узких modules.

## Assembly registration

Generated assembly registration - recommended default для больших приложений:

```csharp
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
```

Для этого нужен `IWF.TeleFlow.Generators`. Отсутствие generated metadata считается startup configuration error.
