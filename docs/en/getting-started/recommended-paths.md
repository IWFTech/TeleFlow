# Recommended Paths

TeleFlow can be used as a tiny bot library or as a framework for a larger service. The right path depends on the user, not on the package name.

## If You Are New

Start with the framework long polling package:

```bash
dotnet add package IWF.TeleFlow.Telegram.Framework.LongPolling --prerelease
dotnet add package IWF.TeleFlow.Generators --prerelease
dotnet add package IWF.TeleFlow.Storage.Memory --prerelease
```

Use this shape:

```csharp
builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddMemoryStateStorage();
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();
```

Write one class per use case:

```csharp
public sealed class StartHandler
{
    [Command("start")]
    public Task Handle(MessageContext ctx, CancellationToken ct)
    {
        return ctx.Message.AnswerAsync("Choose an action.", ct);
    }
}
```

Do not start with custom middleware, custom storage, webhook deployment, or reflection registration. Those are useful later, but they slow down the first bot.

## If You Are Building A Real Product

Keep the framework model, but split responsibilities:

```text
Bot/
  Program.cs
  Handlers/
  Scenes/
  Filters/
Application/
  Services/
  Repositories/
Domain/
  Models/
Infrastructure/
  Storage/
  Telegram/
```

Recommended defaults:

- use generated registration;
- keep `IWF.TeleFlow.Generators` private;
- pass `CancellationToken` to I/O;
- keep Telegram-specific code in bot adapters and handlers;
- keep domain services free from Telegram DTOs when possible;
- replace memory storage before multi-instance deployment;
- add tests around handlers and state transitions.

## If You Need Enterprise Predictability

Prefer explicit boundaries and observable failure:

- use generated registration for assembly discovery;
- use direct registration in tests and narrow modules;
- avoid reflection registration unless the application intentionally accepts it;
- treat state storage as infrastructure, not as an in-memory convenience;
- keep transport choice documented;
- verify package graph in CI;
- run tests against generated registration and direct registration where behavior matters;
- keep Bot API calls visible through `ITelegramClient`.

## Choosing Long Polling Or Webhooks

Use long polling when:

- you are developing locally;
- the bot is small or internal;
- you do not want public inbound HTTP endpoints;
- operational simplicity matters more than webhook infrastructure.

Use webhooks when:

- the bot already runs inside ASP.NET Core;
- inbound HTTP infrastructure is available;
- you want Telegram to push updates to your service;
- the deployment platform makes long-running polling workers inconvenient.

Use raw transports when:

- you want Telegram `Update` values directly;
- you do not want TeleFlow handlers;
- another service owns dispatching or queues.

## What TeleFlow Recommends

For most users:

1. Start with `TeleFlow.Telegram.Framework.LongPolling`.
2. Use generated registration.
3. Use memory storage only while the bot is single-process.
4. Move to webhooks only when deployment requirements justify it.
5. Keep direct `ctx.Bot.*Async` calls for Telegram-specific actions instead of hiding the entire Bot API behind your own wrappers.
