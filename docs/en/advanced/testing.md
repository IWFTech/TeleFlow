# Testing

TeleFlow handlers are normal C# classes, so most application behavior should be testable without Telegram.

## What To Test

Test:

- application services separately from Telegram;
- handler routing assumptions through integration-style tests;
- state transitions;
- callback payload behavior;
- error handlers that change user-visible behavior;
- custom filters;
- custom storage implementations.

Do not test Telegram itself.

## Keep Business Logic Out Of Handlers

Handlers should translate Telegram updates into application calls:

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

Then test `TicketService` with ordinary unit tests.

## Use Direct Registration In Tests

Direct registration keeps narrow tests readable:

```csharp
services.AddTelegramBot(options => options.Token = "test");
services.AddTelegramHandler<TicketHandler>();
services.AddMemoryStateStorage();
```

Generated registration is worth testing for application startup or package-level smoke tests.

## Fake Telegram Client

For handler tests, replace `ITelegramClient`:

```csharp
builder.Services.AddTelegramClient<FakeTelegramClient>();
```

Use a fake client to record outgoing Bot API calls and assert behavior.

## State Tests

Stateful flows should be tested as sequences:

1. send `/register`;
2. send name;
3. send age;
4. assert state cleared;
5. assert expected messages or repository writes.

This catches routing, state, and data issues better than testing each handler in isolation.

## Generated Path Tests

For production apps, add at least one startup test that uses:

```csharp
services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
```

That protects against accidentally removing `TeleFlow.Generators` or breaking generated metadata.
