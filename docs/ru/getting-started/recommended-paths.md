# Рекомендуемые пути

TeleFlow можно использовать как маленькую bot library или как framework для большого сервиса. Правильный путь зависит от пользователя, а не от названия пакета.

## Если ты только начинаешь

Начинай с framework long polling package:

```bash
dotnet add package IWF.TeleFlow.Telegram.Framework.LongPolling
dotnet add package IWF.TeleFlow.Generators
dotnet add package IWF.TeleFlow.Storage.Memory
```

Используй такую форму:

```csharp
builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddMemoryStateStorage();
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();
```

Пиши один class на один use case:

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

Не начинай с custom middleware, custom storage, webhook deployment или reflection registration. Это полезно позже, но замедляет первый бот.

## Если ты делаешь реальный продукт

Оставляй framework model, но разделяй ответственность:

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

Рекомендуемые дефолты:

- используй generated registration;
- держи `IWF.TeleFlow.Generators` private-зависимостью;
- передавай `CancellationToken` в I/O;
- держи Telegram-specific code в bot adapters и handlers;
- по возможности не протаскивай Telegram DTOs в domain services;
- замени memory storage перед multi-instance deployment;
- добавляй tests на handlers и state transitions.

## Если нужна enterprise-предсказуемость

Выбирай явные границы и observable failure:

- используй generated registration для assembly discovery;
- используй direct registration в tests и узких modules;
- не используй reflection registration, если приложение не принимает это осознанно;
- считай state storage инфраструктурой, а не удобной in-memory деталью;
- документируй transport choice;
- проверяй package graph в CI;
- тестируй generated registration и direct registration там, где поведение критично;
- оставляй Bot API calls видимыми через `ITelegramClient`.

## Long polling или webhooks

Long polling подходит, когда:

- ты разрабатываешь локально;
- бот маленький или внутренний;
- не хочется поднимать публичный inbound HTTP endpoint;
- operational simplicity важнее webhook infrastructure.

Webhooks подходят, когда:

- бот уже живёт внутри ASP.NET Core;
- inbound HTTP infrastructure уже есть;
- ты хочешь, чтобы Telegram пушил updates в сервис;
- deployment platform неудобна для long-running polling workers.

Raw transports подходят, когда:

- тебе нужны Telegram `Update` values напрямую;
- ты не хочешь TeleFlow handlers;
- другой сервис владеет dispatching или queues.

## Что рекомендует TeleFlow

Для большинства пользователей:

1. Начни с `TeleFlow.Telegram.Framework.LongPolling`.
2. Используй generated registration.
3. Используй memory storage только пока бот single-process.
4. Переходи на webhooks только когда этого требует deployment.
5. Оставляй прямые `ctx.Bot.*Async` вызовы для Telegram-specific actions вместо того, чтобы прятать весь Bot API за своими wrappers.
