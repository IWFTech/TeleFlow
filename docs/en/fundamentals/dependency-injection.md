# Dependency Injection

TeleFlow uses `Microsoft.Extensions.DependencyInjection`. Handlers, filters, middleware, repositories, services, storage implementations, serializers, and Telegram client replacements are registered in the normal .NET service collection.

## Inject Services Into Handlers

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

Both styles are useful. Constructor injection is better for stable dependencies. Parameter injection is convenient for narrow handler methods.

## Register Application Services

```csharp
builder.Services.AddSingleton<ITicketRepository, InMemoryTicketRepository>();
builder.Services.AddSingleton<IStatsService, StatsService>();
builder.Services.AddSingleton<BusinessHoursFilter>();
```

Register application services before `Build()`.

## Avoid Service Locator Handlers

`TelegramUpdateContext.Services` exposes the service provider because framework internals and advanced scenarios need it. Normal handler code should use constructor or parameter injection instead.

Prefer this:

```csharp
public TicketHandler(ITicketRepository tickets)
{
    _tickets = tickets;
}
```

Over this:

```csharp
var tickets = ctx.Services.GetRequiredService<ITicketRepository>();
```

The explicit dependency is easier to test and easier to review.

## Replacing Framework Policies

Use policy replacement APIs when you own infrastructure:

```csharp
builder.Services.AddStateStore<RedisStateStore>();
builder.Services.AddStateDataStore<RedisStateDataStore>();
builder.Services.AddStateHistoryStore<RedisStateHistoryStore>();
builder.Services.AddCallbackDataSerializer<CompactCallbackDataSerializer>();
builder.Services.AddUpdateRateLimiter<TenantUpdateRateLimiter>();
```

Use these intentionally. A replacement point is not a reason to customize everything.

## Lifetime Guidance

- Use singleton for stateless services and thread-safe repositories.
- Use singleton for in-memory demo repositories only when process-local data is acceptable.
- Use scoped services for update-scoped application work. TeleFlow creates one DI scope per update when the update goes through the framework pipeline, and scoped middleware plus handlers share that scope.
- Avoid mutable singleton state for production workflows unless it is explicitly synchronized and process-local by design.

TeleFlow itself does not turn DI into an application architecture. Keep your own boundaries clear.
