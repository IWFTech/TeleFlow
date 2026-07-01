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

## Current Update In Application Services

Handlers should stay close to Telegram: they receive `MessageContext`, `CallbackQueryContext`, route values, callback payloads, and cancellation tokens. Application services should not need to accept handler contexts just to know who is calling the bot.

Use `ITelegramCurrentUpdateAccessor` when a scoped application service needs the current Telegram identity:

```csharp
public sealed class UserActivityService(
    ITelegramCurrentUpdateAccessor current,
    IUserRepository users)
{
    public async Task TouchAsync(CancellationToken ct)
    {
        var user = current.User
            ?? throw new InvalidOperationException("This operation requires a Telegram user.");

        await users.TouchAsync(user.Id, ct);
    }
}
```

Then keep the handler small:

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

The accessor is scoped to one update. It is available while TeleFlow processes an update and fails clearly outside update processing. Do not inject it into singleton services.

## Register Application Services

```csharp
builder.Services.AddScoped<ITicketRepository, EfTicketRepository>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<UserActivityService>();
builder.Services.AddSingleton<BusinessHoursFilter>();
```

Register application services before `Build()`.

TeleFlow validates handler method dependencies, error handler dependencies, and custom filter registrations before normal update processing starts. If a handler asks for `ITicketRepository` and the service is missing, the application fails with a TeleFlow configuration error instead of failing later on a random user update.

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
- Use scoped services for repositories, database contexts, units of work, and application services that belong to one update.
- Handlers are transient by default. They are resolved from the current update scope, so they can safely use scoped constructor dependencies.
- Middleware registered through `AddUpdateMiddleware<T>()` is scoped by default and can safely use scoped repositories and services.
- `ITelegramCurrentUpdateAccessor` and `IUpdateContextAccessor` are scoped current-update accessors. Use them from scoped services, not from singleton services.
- TeleFlow creates one DI scope per update when the update goes through the framework pipeline, and scoped middleware plus handlers share that scope.
- Avoid mutable singleton state for production workflows unless it is explicitly synchronized and process-local by design.

TeleFlow itself does not turn DI into an application architecture. Keep your own boundaries clear.
