# Модель приложения

TeleFlow applications используют ту же форму, что и обычные .NET services:

1. создать application builder;
2. зарегистрировать services;
3. зарегистрировать Telegram framework services;
4. зарегистрировать handlers;
5. зарегистрировать один update source;
6. собрать и запустить приложение.

```csharp
var builder = TeleFlowApplication.CreateBuilder(args);

builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddMemoryStateStorage();
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();

await using var app = builder.Build();
await app.RunAsync();
```

## Основные понятия

### Update source

Update source производит updates. Framework long polling и framework webhooks являются update sources. В running application должен быть один `IUpdateSource`.

### Update processor

Update processor создаёт `UpdateContext`, запускает middleware и передаёт update в dispatcher.

### Middleware

Middleware оборачивает выполнение update. Built-in middleware отвечает за logging, exception handling, state и rate limiting там, где они зарегистрированы.

### Dispatcher

Dispatcher выбирает и вызывает подходящий handler. Telegram-specific dispatching находится вне `TeleFlow.Core`.

### Handler

Handler - обычный class с method, который TeleFlow может вызвать. Routing metadata задаётся атрибутами.

## Telegram contexts

Telegram handlers получают context objects, в которых есть текущий update, Telegram client, state и небольшие action helpers.

Общие свойства из `TelegramUpdateContext`:

| Property | Что означает |
| --- | --- |
| `Bot` | Low-level `ITelegramClient`. Используй его для полного доступа к Bot API. |
| `Update` | Raw Telegram `Update`. |
| `State` | Facade текущего state. |
| `Wizard` | Wizard navigation поверх state history. |
| `Chat` | Helper для `typing`, upload actions и похожих indicators. |
| `CancellationToken` | Framework cancellation token текущего update. |
| `Services` | Service provider для advanced scenarios. В обычном handler code лучше DI parameters или constructor injection. |

Message handlers используют `MessageContext`:

```csharp
[Command("whoami")]
public Task WhoAmI(MessageContext ctx, CancellationToken ct)
{
    var id = ctx.Sender?.Id;
    var name = ctx.User?.FullName ?? "unknown";
    return ctx.Message.AnswerAsync($"User: {name}, id: {id}", ct);
}
```

`ctx.Message` содержит message actions: `AnswerAsync`, `ReplyAsync`, `AnswerPhotoAsync`, `ReplyDocumentAsync`, `DeleteAsync`. Эти helpers нацелены на current chat. Используй `ctx.Bot.*Async`, когда target chat или method surface должны быть явными.

Callback handlers используют `CallbackQueryContext`:

```csharp
[Callback]
public async Task Handle(CallbackQueryContext ctx, CancellationToken ct)
{
    await ctx.Callback.AnswerAsync(ct);
    await ctx.Callback.EditTextAsync("Done.", ct);
}
```

Chat member handlers используют `ChatMemberUpdatedContext`:

```csharp
[ChatMemberUpdated]
public Task Audit(ChatMemberUpdatedContext ctx, IAuditLog audit, CancellationToken ct)
{
    return audit.RecordAsync(ctx.TelegramChat.Id, ctx.Member.Id, ct);
}
```

`ctx.Chat.ActionAsync(...)` сразу отправляет chat action и поддерживает его, пока не будет disposed возвращённый lease:

```csharp
await using var typing = await ctx.Chat.ActionAsync(ChatAction.Typing, ct);
await reportService.BuildAsync(ct);
await ctx.Message.AnswerAsync("Report is ready.", ct);
```

## Почему `TeleFlow.Core` transport-agnostic

`TeleFlow.Core` владеет application, middleware, update processing, state contracts и replacement points. Он не знает про Telegram message fields, callbacks, Bot API methods или Telegram update types.

Telegram behavior живёт в Telegram-пакетах:

- `TeleFlow.Telegram.Client`
- `TeleFlow.Telegram.Framework`
- `TeleFlow.Telegram.Framework.LongPolling`
- `TeleFlow.Telegram.Framework.Webhooks`
- raw transport packages

Так dependency direction остаётся чистым, а framework проще тестировать.

## Startup failure - это намеренно

TeleFlow предпочитает ранние configuration errors вместо best-effort ambiguity. Примеры:

- `AddTelegramBot(...)` должен быть вызван до framework handlers и transports.
- `AddLongPolling(...)` и `AddWebhook(...)` не должны одновременно владеть `IUpdateSource`.
- `AddTelegramHandlersFromAssembly(...)` требует generated metadata.
- Options валидируются во время регистрации.

Это намеренное поведение. Бот должен падать на startup, если configuration wrong, а не после первого production update.

## Минимальное direct client app

Если handlers не нужны, используй client package напрямую:

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

## Минимальное framework app

Framework нужен, когда нужны routing, filters, callbacks, state и transports:

```csharp
builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();
```

## Замена core policies

TeleFlow exposes replacement APIs для реальных extension points:

```csharp
services.AddUpdateSource<MyUpdateSource>();
services.AddUpdateDispatcher<MyDispatcher>();
services.AddCallbackDataSerializer<MySerializer>();
services.AddStateStore<MyStateStore>();
services.AddStateDataStore<MyStateDataStore>();
services.AddStateHistoryStore<MyHistoryStore>();
services.AddStateKeyFactory<MyStateKeyFactory>();
```

Это advanced APIs. Большинству приложений лучше начать с framework transports и memory storage, а потом заменить только то, что стало инфраструктурным требованием.
