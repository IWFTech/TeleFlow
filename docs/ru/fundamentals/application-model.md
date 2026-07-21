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

Если приложение уже использует Microsoft.Extensions.Hosting, подключай optional hosting adapter вместо ручного `Build()` у `TeleFlowApplication`:

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

Hosted service использует service provider от host-а. Он не создаёт второй DI container и не владеет provider disposal. Не используй hosted service для ASP.NET Core webhook apps: webhook processing управляется endpoint routing.

## Основные понятия

### Update source

Update source производит updates. Framework long polling и framework webhooks являются update sources. В running application должен быть один `IUpdateSource`.

### Update processor

Update processor создаёт `UpdateContext`, запускает middleware и передаёт update в dispatcher.

### Middleware

Middleware оборачивает выполнение update. Built-in middleware отвечает за logging, exception handling, state и rate limiting там, где они зарегистрированы.

### Dispatcher

Dispatcher выбирает и вызывает подходящий handler. Telegram-specific dispatching находится вне `TeleFlow.Framework.Core`.

### Handler

Handler - обычный class с method, который TeleFlow может вызвать. Routing metadata задаётся атрибутами.

## Application lifecycle

TeleFlow lifecycle tasks выполняются вокруг update source:

1. startup tasks запускаются в порядке регистрации;
2. update source стартует и обрабатывает updates;
3. shutdown tasks запускаются в обратном порядке регистрации после остановки update source.

Используй lifecycle tasks для startup/shutdown работы, которая принадлежит процессу бота:

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
                BotCommands.Create("start", "Start"),
                BotCommands.Ephemeral("help", "Показать личную справку")
            ],
            cancellationToken: ct);
    }
}
```

Lifecycle tasks не являются Telegram handlers. Они не получают `MessageContext`, `CallbackQueryContext` или fake updates. TeleFlow создаёт их через dependency injection в отдельном lifecycle scope, поэтому scoped application services можно использовать безопасно.

`BotCommands` явно задаёт смысл команды. `Create(...)` создаёт обычную команду, а `Ephemeral(...)` выставляет Telegram-флаг `is_ephemeral`: в группе или супергруппе команда и её ответ видны только вызвавшему пользователю. Этот helper не ищет handlers и не публикует команды автоматически. Telegram по-прежнему принимает для меню только имена команд по правилам Bot API, поэтому используй строчное английское имя вроде `help`, а локальные алиасы добавляй отдельно.

Если startup task падает, update processing не стартует. Если update source падает уже после успешного startup, shutdown tasks всё равно выполняются, а исходная ошибка пробрасывается наружу. Если shutdown тоже падает, TeleFlow сообщает обе ошибки.

Не используй lifecycle tasks для long-running background jobs. Для отдельной фоновой работы используй обычные .NET primitives: `IHostedService` или `BackgroundService`.

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

`ctx.Message` содержит message actions: `AnswerAsync`, `ReplyAsync`, `AnswerPhotoAsync`, `ReplyDocumentAsync`, `SendEphemeralAsync`, `DeleteAsync`. Эти helpers нацелены на current chat. Используй `ctx.Bot.*Async`, когда target chat или method surface должны быть явными.

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

## Почему `TeleFlow.Framework.Core` transport-agnostic

`TeleFlow.Framework.Core` владеет application, middleware, update processing, state contracts и replacement points. Он не знает про Telegram message fields, callbacks, Bot API methods или Telegram update types.

Telegram behavior живёт в Telegram-пакетах:

- `TeleFlow.Telegram.Client`
- `TeleFlow.Framework`
- `TeleFlow.Framework.LongPolling`
- `TeleFlow.Framework.Webhooks`
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
services.AddTeleFlowStartupTask<MyStartupTask>();
services.AddTeleFlowShutdownTask<MyShutdownTask>();
```

Это advanced APIs. Большинству приложений лучше начать с framework transports и memory storage, а потом заменить только то, что стало инфраструктурным требованием.
