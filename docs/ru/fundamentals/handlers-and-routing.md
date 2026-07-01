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

Матчинг command prefix явный. По умолчанию используется
`CommandPrefixMode.Required`: пользователь должен отправить `/start` или другую
настроенную prefix-команду:

```csharp
[Command("start", Prefixes = new[] { "/", "!" })]
public Task Start(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Welcome.", ct);
}
```

Если один route должен принимать и Telegram command, и текст без prefix,
используй `CommandPrefixMode.Optional`:

```csharp
[Command("help", PrefixMode = CommandPrefixMode.Optional)]
public Task Help(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Help.", ct);
}
```

Если нужна command route-семантика, но только для текста без prefix, используй
`CommandPrefixMode.NoPrefix`:

```csharp
[Command("help", PrefixMode = CommandPrefixMode.NoPrefix)]
public Task HelpText(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Help.", ct);
}
```

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

Template описывает command body, а не prefix. Пиши
`[CommandTemplate("ticket {id:long}")]`, а не
`[CommandTemplate("/ticket {id:long}")]`.

Если нужно поддержать и `/ticket 42`, и `ticket 42`, оставь один handler и
сделай prefix optional:

```csharp
[CommandTemplate(
    "ticket {id:long}",
    PrefixMode = CommandPrefixMode.Optional)]
public Task ShowTicket(MessageContext ctx, long id, CancellationToken ct)
{
    return ctx.Message.AnswerAsync($"Ticket #{id}", ct);
}
```

Это заменяет старый workaround, где на один method вешались и
`[CommandTemplate]`, и `[TextTemplate]`.

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

Routes и filters оба используют C# attribute syntax. Route attributes вроде `[CommandTemplate]` выбирают handler и bind-ят values. Filter attributes вроде `[HasPhoto]`, `[ChatType]` и `[UseFilter<TFilter>]` добавляют условия перед вызовом. Подробнее разница описана в разделе [Фильтры](filters.md).

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

Используй её для маленьких apps, tests или узких modules. Direct registration
регистрирует только указанный handler или module type и не сканирует assembly
вокруг него.

## Assembly registration

Generated assembly registration - recommended default для больших приложений:

```csharp
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
```

Для этого нужен `IWF.TeleFlow.Generators`. Отсутствие generated metadata считается startup configuration error.
