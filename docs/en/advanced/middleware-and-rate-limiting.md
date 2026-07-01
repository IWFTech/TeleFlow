# Middleware And Rate Limiting

TeleFlow update execution goes through middleware. Middleware is a good place for cross-cutting runtime behavior.

## Register Middleware

```csharp
builder.Services.AddUpdateMiddleware<MyMiddleware>();
```

Middleware implements `IUpdateMiddleware`.

Use middleware for:

- logging enrichment;
- tracing;
- tenant resolution;
- update-level guards;
- rate limiting;
- state initialization;
- exception policies.

Avoid middleware for handler-specific business logic.

## User Stats And Anti-Spam

Update middleware runs before Telegram routing, built-in filters, custom filters, and handler invocation. This makes it a good place for global checks that apply to most updates.

This example records an incoming Telegram user, increments update statistics, and stops the pipeline when the user is blocked:

```csharp
using TeleFlow.Framework.Middleware;
using TeleFlow.Framework.Updates;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Types;

public sealed class UserGateMiddleware(
    IUserRepository users,
    IAntiSpamService antiSpam,
    IUpdateStatistics stats) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext context, UpdateDelegate next)
    {
        if (!context.TryGetTelegramUpdate(out var update))
        {
            await next(context);
            return;
        }

        var actor = TryGetActor(update);
        if (actor is null)
        {
            await next(context);
            return;
        }

        await users.EnsureExistsAsync(actor.UserId, context.CancellationToken);
        await stats.RecordIncomingUpdateAsync(
            update.UpdateId,
            actor.UserId,
            actor.ChatId,
            context.CancellationToken);

        if (await antiSpam.IsBlockedAsync(actor.UserId, context.CancellationToken))
        {
            return;
        }

        await next(context);
    }

    private static TelegramActor? TryGetActor(Update update)
    {
        var message = update.Message ??
            update.EditedMessage ??
            update.BusinessMessage ??
            update.EditedBusinessMessage ??
            update.ChannelPost ??
            update.EditedChannelPost;

        if (message?.From is { } sender)
        {
            return new TelegramActor(sender.Id, message.Chat.Id);
        }

        if (update.CallbackQuery is { } callback)
        {
            return new TelegramActor(callback.From.Id, ChatId: null);
        }

        var memberUpdate = update.ChatMember ?? update.MyChatMember;
        if (memberUpdate is not null)
        {
            return new TelegramActor(memberUpdate.From.Id, memberUpdate.Chat.Id);
        }

        return null;
    }

    private sealed record TelegramActor(long UserId, long? ChatId);
}
```

Register the middleware and its dependencies:

```csharp
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAntiSpamService, AntiSpamService>();
builder.Services.AddScoped<IUpdateStatistics, UpdateStatistics>();

builder.Services.AddUpdateMiddleware<UserGateMiddleware>();
```

`AddUpdateMiddleware<T>()` registers middleware in the update pipeline and creates the middleware from the current update scope. This means middleware can use scoped constructor dependencies such as repositories, unit-of-work services, database contexts, and request/update-scoped application services.

Use the normal .NET options pattern when middleware needs application configuration. In a minimal console project, add the options package explicitly:

```bash
dotnet add package Microsoft.Extensions.Options
```

```csharp
using Microsoft.Extensions.Options;

public sealed class UserGateOptions
{
    public bool RecordStatistics { get; set; } = true;

    public TimeSpan BlockCacheWindow { get; set; } = TimeSpan.FromSeconds(15);
}

builder.Services.Configure<UserGateOptions>(options =>
{
    options.RecordStatistics = true;
    options.BlockCacheWindow = TimeSpan.FromSeconds(15);
});
```

Then inject `IOptions<TOptions>` into the middleware constructor:

```csharp
public sealed class UserGateMiddleware(
    IUserRepository users,
    IAntiSpamService antiSpam,
    IOptions<UserGateOptions> options) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext context, UpdateDelegate next)
    {
        var configuration = options.Value;

        // Use configuration together with scoped services.

        await next(context);
    }
}
```

Do not pass pre-created middleware instances to the pipeline. Let the container construct middleware so dependency lifetimes, disposal, and scoped services stay predictable.

Use `context.Services` only when a middleware intentionally needs dynamic service resolution. Constructor injection is the recommended path for normal application dependencies.

If a middleware is stateless and must be reused as one process-wide instance, register it explicitly:

```csharp
builder.Services.AddSingletonUpdateMiddleware<MyStatelessMiddleware>();
```

Singleton middleware must not depend on scoped services in its constructor.

Do not register middleware directly as `IUpdateMiddleware` in `IServiceCollection`. TeleFlow builds the middleware pipeline from middleware registrations, not from arbitrary `IUpdateMiddleware` services, and application startup fails clearly when direct registrations are found.

If a middleware does not call `next(context)`, TeleFlow stops processing that update. Routing and handlers will not run. Use this for global gates such as ban lists, tenant shutdown, or hard rate limits.

Use a filter instead when the check belongs to one handler or one handler group. Use handler services when the logic is part of one concrete user workflow.

## Rate Limiting

TeleFlow exposes `IUpdateRateLimiter` and default registration helpers:

```csharp
builder.Services.AddDefaultUpdateRateLimiting();
builder.Services.AddUpdateRateLimiter<MyRateLimiter>();
```

The default limiter is no-op. Custom limiters are application-specific.

## Ordering

Middleware order matters. Register middleware deliberately and keep the order documented when behavior depends on it.

For example:

1. logging or tracing;
2. tenant resolution;
3. rate limiting;
4. state;
5. dispatch.

Do not hide essential business rules in middleware ordering. If the rule is core application behavior, make it visible in services or filters.

## Enterprise Guidance

Middleware is a framework-level tool. Treat each middleware as infrastructure with tests and clear ownership.

Good middleware is boring:

- clear inputs;
- clear outputs;
- observable failures;
- no hidden handler selection;
- no unexpected Telegram side effects.
