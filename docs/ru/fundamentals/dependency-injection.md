# Dependency Injection

TeleFlow использует `Microsoft.Extensions.DependencyInjection`. Хэндлеры, фильтры, middleware, репозитории, сервисы, storage-реализации, сериализаторы и замены Telegram client регистрируются в обычном .NET service collection.

## Внедрение сервисов в хэндлеры

Constructor injection:

```csharp
public sealed class TicketHandler
{
    private readonly ITicketRepository _tickets;

    public TicketHandler(ITicketRepository tickets)
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

Parameter injection:

```csharp
[Command("stats")]
public async Task Stats(
    MessageContext ctx,
    IStatsService stats,
    CancellationToken ct)
{
    var snapshot = await stats.GetSnapshotAsync(ct);
    await ctx.Message.AnswerAsync(snapshot.ToString(), ct);
}
```

Оба стиля полезны. Constructor injection лучше для стабильных зависимостей. Parameter injection удобен для узких handler methods.

## Текущий update в application service

Хэндлер должен оставаться рядом с Telegram: он принимает `MessageContext`, `CallbackQueryContext`, route values, callback payloads и cancellation token. Но application service не должен принимать handler context только ради того, чтобы узнать текущего пользователя или чат.

Если scoped application service нужен текущий Telegram user/chat/message, используй `ITelegramCurrentUpdateAccessor`:

```csharp
public sealed class UserActivityService(
    ITelegramCurrentUpdateAccessor current,
    IUserRepository users)
{
    public async Task TouchAsync(CancellationToken ct)
    {
        var user = current.User
            ?? throw new InvalidOperationException("Для этой операции нужен Telegram user.");

        await users.TouchAsync(user.Id, ct);
    }
}
```

Хэндлер при этом остаётся тонким:

```csharp
public sealed class StartHandler(UserActivityService activity)
{
    [Command("start")]
    public async Task Start(MessageContext ctx, CancellationToken ct)
    {
        await activity.TouchAsync(ct);
        await ctx.Message.AnswerAsync("Ready.", ct);
    }
}
```

Accessor живёт в scope одного update. Он доступен только пока TeleFlow обрабатывает update, а вне обработки падает с понятной ошибкой. Не внедряй его в singleton services.

## Регистрация application services

```csharp
builder.Services.AddScoped<ITicketRepository, EfTicketRepository>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<UserActivityService>();
builder.Services.AddSingleton<BusinessHoursFilter>();
```

Application services регистрируются до `Build()`.

TeleFlow валидирует зависимости handler methods, error handlers и custom filters до нормальной обработки updates. Если handler просит `ITicketRepository`, а сервис не зарегистрирован, приложение упадёт с понятной TeleFlow configuration error, а не позже на случайном update от пользователя.

## Не превращай handler в service locator

`TelegramUpdateContext.Services` exposes service provider, потому что он нужен framework internals и advanced scenarios. В нормальном handler code лучше использовать constructor или parameter injection.

Лучше так:

```csharp
public TicketHandler(ITicketRepository tickets)
{
    _tickets = tickets;
}
```

Чем так:

```csharp
var tickets = ctx.Services.GetRequiredService<ITicketRepository>();
```

Явная зависимость проще тестируется и проще ревьюится.

## Replacing framework policies

Policy replacement APIs нужны, когда ты владеешь инфраструктурой:

```csharp
builder.Services.AddStateStore<RedisStateStore>();
builder.Services.AddStateDataStore<RedisStateDataStore>();
builder.Services.AddStateHistoryStore<RedisStateHistoryStore>();
builder.Services.AddCallbackDataSerializer<CompactCallbackDataSerializer>();
builder.Services.AddUpdateRateLimiter<TenantUpdateRateLimiter>();
```

Используй это осознанно. Наличие replacement point не означает, что нужно кастомизировать всё.

## Рекомендации по lifetimes

- Singleton подходит для stateless services и thread-safe repositories.
- Singleton подходит для in-memory demo repositories только когда process-local data acceptable.
- Scoped services используй для repositories, database contexts, units of work и application services, которые относятся к одному update.
- Хэндлеры по умолчанию transient. Они создаются из текущего update scope, поэтому могут безопасно принимать scoped dependencies в constructor.
- Middleware, зарегистрированное через `AddUpdateMiddleware<T>()`, по умолчанию scoped и может безопасно принимать scoped repositories и services.
- `ITelegramCurrentUpdateAccessor` и `IUpdateContextAccessor` - scoped current-update accessors. Используй их из scoped services, а не из singleton services.
- TeleFlow создаёт один DI scope на update, когда update проходит через framework pipeline, и scoped middleware вместе с handlers разделяют этот scope.
- Избегай mutable singleton state в production workflows, если он явно не синхронизирован и не process-local by design.

TeleFlow сам по себе не превращает DI в application architecture. Границы своего приложения нужно держать отдельно.
