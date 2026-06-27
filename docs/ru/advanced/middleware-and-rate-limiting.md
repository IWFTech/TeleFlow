# Middleware и rate limiting

TeleFlow update execution проходит через middleware. Middleware подходит для cross-cutting runtime behavior.

## Регистрация middleware

```csharp
builder.Services.AddUpdateMiddleware<MyMiddleware>();
```

Middleware implements `IUpdateMiddleware`.

Используй middleware для:

- logging enrichment;
- tracing;
- tenant resolution;
- update-level guards;
- rate limiting;
- state initialization;
- exception policies.

Не используй middleware для handler-specific business logic.

## User stats и anti-spam

Update middleware выполняется до Telegram routing, built-in filters, custom filters и вызова handler. Поэтому это нормальное место для глобальных checks, которые относятся ко всем или почти всем updates.

Этот пример регистрирует входящего Telegram user, пишет статистику update-а и останавливает pipeline, если user заблокирован:

```csharp
using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Core.Middleware;
using TeleFlow.Core.Updates;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Types;

public sealed class UserGateMiddleware : IUpdateMiddleware
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

        var users = context.Services.GetRequiredService<IUserRepository>();
        var antiSpam = context.Services.GetRequiredService<IAntiSpamService>();
        var stats = context.Services.GetRequiredService<IUpdateStatistics>();

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

Зарегистрируй middleware и его dependencies:

```csharp
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAntiSpamService, AntiSpamService>();
builder.Services.AddScoped<IUpdateStatistics, UpdateStatistics>();

builder.Services.AddUpdateMiddleware<UserGateMiddleware>();
```

`AddUpdateMiddleware<T>()` регистрирует middleware как singleton. Scoped application services резолви из `context.Services`: это service provider текущего update scope.

Если middleware не вызывает `next(context)`, TeleFlow прекращает обработку этого update. Routing и handlers не выполнятся. Используй это для глобальных gates: ban list, tenant shutdown или жёсткие rate limits.

Если check относится к одному handler или группе handlers, лучше используй filter. Если logic относится к конкретному user workflow, держи её в handler service.

## Rate limiting

TeleFlow exposes `IUpdateRateLimiter` и default registration helpers:

```csharp
builder.Services.AddDefaultUpdateRateLimiting();
builder.Services.AddUpdateRateLimiter<MyRateLimiter>();
```

Default limiter - no-op. Custom limiters application-specific.

## Порядок выполнения

Middleware order matters. Регистрируй middleware осознанно и документируй order, если behavior от него зависит.

Пример:

1. logging or tracing;
2. tenant resolution;
3. rate limiting;
4. state;
5. dispatch.

Не прячь essential business rules в middleware ordering. Если rule - core application behavior, делай его видимым в services или filters.

## Enterprise-рекомендации

Middleware - framework-level tool. Относись к каждому middleware как к infrastructure с tests и clear ownership.

Хороший middleware скучный:

- clear inputs;
- clear outputs;
- observable failures;
- no hidden handler selection;
- no unexpected Telegram side effects.
