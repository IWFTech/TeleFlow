# Тестирование

TeleFlow handlers - обычные C# classes, поэтому большую часть application behavior можно тестировать без Telegram.

## Что тестировать

Тестируй:

- application services отдельно от Telegram;
- handler routing assumptions через integration-style tests;
- state transitions;
- callback payload behavior;
- error handlers, которые меняют user-visible behavior;
- custom filters;
- custom storage implementations.

Не тестируй сам Telegram.

## Держи бизнес-логику вне хэндлеров

Handlers должны переводить Telegram updates в application calls:

```csharp
public sealed class TicketHandler
{
    private readonly TicketService _tickets;

    public TicketHandler(TicketService tickets)
    {
        _tickets = tickets;
    }

    [CommandTemplate("ticket {id:long}")]
    public async Task Handle(MessageContext ctx, long id, CancellationToken ct)
    {
        var ticket = await _tickets.GetAsync(id, ct);
        await ctx.Message.AnswerAsync(ticket.Title, ct);
    }
}
```

Потом тестируй `TicketService` обычными unit tests.

## Используй прямую регистрацию в тестах

Direct registration делает узкие tests readable:

```csharp
services.AddTelegramBot(options => options.Token = "test");
services.AddTelegramHandler<TicketHandler>();
services.AddMemoryStateStorage();
```

Generated registration стоит тестировать для application startup или package-level smoke tests.

## Фейковый Telegram client

Для handler tests заменяй `ITelegramClient`:

```csharp
builder.Services.AddTelegramClient<FakeTelegramClient>();
```

Fake client записывает outgoing Bot API calls и позволяет assert behavior.

## Тесты state

Stateful flows тестируй sequences:

1. send `/register`;
2. send name;
3. send age;
4. assert state cleared;
5. assert expected messages or repository writes.

Это лучше ловит routing, state и data issues, чем изолированные tests каждого handler.

## Тесты generated path

Для production apps добавь хотя бы один startup test с:

```csharp
services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
```

Это защищает от случайного удаления `IWF.TeleFlow.Generators` или поломки generated metadata.
