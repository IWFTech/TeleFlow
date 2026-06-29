# Dependency Injection

TeleFlow использует `Microsoft.Extensions.DependencyInjection`. Handlers, filters, middleware, repositories, services, storage implementations, serializers и Telegram client replacements регистрируются в обычном .NET service collection.

## Внедрение сервисов в handlers

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

Оба стиля полезны. Constructor injection лучше для стабильных dependencies. Parameter injection удобен для узких handler methods.

## Регистрация application services

```csharp
builder.Services.AddSingleton<ITicketRepository, InMemoryTicketRepository>();
builder.Services.AddSingleton<IStatsService, StatsService>();
builder.Services.AddSingleton<BusinessHoursFilter>();
```

Application services регистрируются до `Build()`.

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
- Scoped services используй для update-scoped application work. TeleFlow создаёт один DI scope на update, когда update проходит через framework pipeline, и scoped middleware вместе с handlers разделяют этот scope.
- Избегай mutable singleton state в production workflows, если он явно не синхронизирован и не process-local by design.

TeleFlow сам по себе не превращает DI в application architecture. Границы своего приложения нужно держать отдельно.
