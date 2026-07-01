# Application Model

TeleFlow applications use the same shape as normal .NET services:

1. create an application builder;
2. register services;
3. register Telegram framework services;
4. register handlers;
5. register one update source;
6. build and run.

```csharp
var builder = TeleFlowApplication.CreateBuilder(args);

builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddMemoryStateStorage();
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();

await using var app = builder.Build();
await app.RunAsync();
```

For applications that already use Microsoft.Extensions.Hosting, register the optional hosting adapter instead of calling `Build()` on `TeleFlowApplication` yourself:

```csharp
using Microsoft.Extensions.Hosting;
using TeleFlow.Framework.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddMemoryStateStorage();
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();
builder.Services.AddTeleFlowHostedService();

await builder.Build().RunAsync();
```

The hosted service uses the host's service provider. It does not create a second DI container and it does not own provider disposal. Do not use the hosted service for ASP.NET Core webhook apps; webhook processing is driven by endpoint routing.

## Core Concepts

### Update Source

An update source produces updates. Framework long polling and framework webhooks are update sources. Only one `IUpdateSource` should be registered for a running application.

### Update Processor

The update processor creates an `UpdateContext`, runs middleware, and dispatches the update.

### Middleware

Middleware wraps update execution. Built-in middleware covers logging, exception handling, state, and rate limiting where registered.

### Dispatcher

The dispatcher selects and invokes the matching handler. Telegram-specific dispatching lives outside `TeleFlow.Framework.Core`.

### Handler

A handler is a normal class with a method that TeleFlow can invoke. Routing metadata comes from attributes.

## Application Lifecycle

TeleFlow lifecycle tasks run around the update source:

1. startup tasks run in registration order;
2. the update source starts and processes updates;
3. shutdown tasks run in reverse registration order after the update source stops.

Use lifecycle tasks for application startup and shutdown work that belongs to the bot process:

```csharp
builder.Services.AddTeleFlowStartupTask<ConfigureBotCommands>();
builder.Services.AddTeleFlowShutdownTask<FlushMetrics>();
```

```csharp
public sealed class ConfigureBotCommands(ITelegramClient bot) : ITeleFlowStartupTask
{
    public async ValueTask ExecuteAsync(CancellationToken ct = default)
    {
        await bot.SetMyCommandsAsync(
            commands:
            [
                new BotCommand
                {
                    Command = "start",
                    Description = "Start"
                }
            ],
            cancellationToken: ct);
    }
}
```

Lifecycle tasks are not Telegram handlers. They do not receive `MessageContext`, `CallbackQueryContext`, or fake updates. They are resolved through dependency injection from a dedicated lifecycle scope, so scoped application services can be used safely.

If a startup task fails, update processing does not start. If the update source fails after startup has completed, shutdown tasks still run and the original failure is surfaced. If shutdown also fails, TeleFlow reports both failures.

Do not use lifecycle tasks for long-running background jobs. Use normal .NET hosting primitives such as `IHostedService` or `BackgroundService` when the work is a separate background process.

## Telegram Contexts

Telegram handlers receive context objects that expose the current update, the Telegram client, state, and small action helpers.

Common properties from `TelegramUpdateContext`:

| Property | Meaning |
| --- | --- |
| `Bot` | The low-level `ITelegramClient`. Use it for full Bot API access. |
| `Update` | The raw Telegram `Update`. |
| `State` | Current update state facade. |
| `Wizard` | Wizard navigation facade over state history. |
| `Chat` | Chat action helper for `typing`, upload actions, and similar indicators. |
| `CancellationToken` | The framework cancellation token for the current update. |
| `Services` | Service provider for advanced scenarios. Prefer DI parameters or constructors in normal handler code. |

Message handlers use `MessageContext`:

```csharp
[Command("whoami")]
public Task WhoAmI(MessageContext ctx, CancellationToken ct)
{
    var id = ctx.Sender?.Id;
    var name = ctx.User?.FullName ?? "unknown";
    return ctx.Message.AnswerAsync($"User: {name}, id: {id}", ct);
}
```

`ctx.Message` contains message actions such as `AnswerAsync`, `ReplyAsync`, `AnswerPhotoAsync`, `ReplyDocumentAsync`, and `DeleteAsync`. These helpers target the current chat. Use `ctx.Bot.*Async` when the target chat or method surface should be explicit.

Callback handlers use `CallbackQueryContext`:

```csharp
[Callback]
public async Task Handle(CallbackQueryContext ctx, CancellationToken ct)
{
    await ctx.Callback.AnswerAsync(ct);
    await ctx.Callback.EditTextAsync("Done.", ct);
}
```

Chat member handlers use `ChatMemberUpdatedContext`:

```csharp
[ChatMemberUpdated]
public Task Audit(ChatMemberUpdatedContext ctx, IAuditLog audit, CancellationToken ct)
{
    return audit.RecordAsync(ctx.TelegramChat.Id, ctx.Member.Id, ct);
}
```

`ctx.Chat.ActionAsync(...)` sends a chat action immediately and keeps it alive until the returned lease is disposed:

```csharp
await using var typing = await ctx.Chat.ActionAsync(ChatAction.Typing, ct);
await reportService.BuildAsync(ct);
await ctx.Message.AnswerAsync("Report is ready.", ct);
```

## Why `TeleFlow.Framework.Core` Is Transport-Agnostic

`TeleFlow.Framework.Core` owns application, middleware, update processing, state contracts, and replacement points. It does not know about Telegram message fields, callbacks, Bot API methods, or Telegram update types.

Telegram behavior lives in Telegram packages:

- `TeleFlow.Telegram.Client`
- `TeleFlow.Framework`
- `TeleFlow.Framework.LongPolling`
- `TeleFlow.Framework.Webhooks`
- raw transport packages

This keeps the dependency direction clean and makes the framework easier to test.

## Startup Failure Is Intentional

TeleFlow prefers early configuration errors over best-effort ambiguity. Examples:

- `AddTelegramBot(...)` must be called before framework handlers and transports.
- `AddLongPolling(...)` and `AddWebhook(...)` cannot both own `IUpdateSource`.
- `AddTelegramHandlersFromAssembly(...)` requires generated metadata.
- Options are validated when they are registered.

This is deliberate. A bot should fail during startup when configuration is wrong, not after the first production update arrives.

## Minimal Direct Client App

If you do not need handlers, use the client package directly:

```csharp
using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Telegram;

var services = new ServiceCollection();

services.AddTelegramClient(options =>
{
    options.Token = token;
});

using var provider = services.BuildServiceProvider();
var bot = provider.GetRequiredService<ITelegramClient>();

var me = await bot.GetMeAsync();
Console.WriteLine(me.Username);
```

## Minimal Framework App

Use the framework when you want routing, filters, callbacks, state, and transports:

```csharp
builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();
```

## Replacing Core Policies

TeleFlow exposes replacement APIs for real extension points:

```csharp
services.AddUpdateSource<MyUpdateSource>();
services.AddUpdateDispatcher<MyDispatcher>();
services.AddCallbackDataSerializer<MySerializer>();
services.AddStateStore<MyStateStore>();
services.AddStateDataStore<MyStateDataStore>();
services.AddStateHistoryStore<MyHistoryStore>();
services.AddStateKeyFactory<MyStateKeyFactory>();
services.AddTeleFlowStartupTask<MyStartupTask>();
services.AddTeleFlowShutdownTask<MyShutdownTask>();
```

These are advanced APIs. Most applications should start with framework transports and memory storage, then replace only the part that has become an infrastructure requirement.
