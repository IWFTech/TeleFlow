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

Зарегистрируй middleware и его dependencies:

```csharp
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAntiSpamService, AntiSpamService>();
builder.Services.AddScoped<IUpdateStatistics, UpdateStatistics>();

builder.Services.AddUpdateMiddleware<UserGateMiddleware>();
```

`AddUpdateMiddleware<T>()` добавляет middleware в update pipeline и создаёт его из текущего update scope. Поэтому middleware может принимать scoped-зависимости в конструкторе: repositories, unit-of-work services, database contexts и другие update-scoped application services.

Middleware получает `UpdateContext` напрямую, потому что middleware является частью framework pipeline. Обычным application services обычно не нужно принимать `UpdateContext` или `MessageContext`. Если scoped service нужен текущий Telegram user или chat, внедри в этот service `ITelegramCurrentUpdateAccessor`.

Если middleware нужна application configuration, используй обычный .NET options pattern. В минимальном console-проекте пакет с options лучше добавить явно:

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

Затем передай `IOptions<TOptions>` в constructor middleware:

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

Не передавай заранее созданные middleware instances в pipeline. Пусть container создаёт middleware сам: так lifetimes, disposal и scoped services остаются предсказуемыми.

`context.Services` используй только когда middleware намеренно нужен dynamic service resolution. Для обычных application dependencies рекомендуемый путь - constructor injection.

Если middleware stateless и должен переиспользоваться как один process-wide instance, регистрируй это явно:

```csharp
builder.Services.AddSingletonUpdateMiddleware<MyStatelessMiddleware>();
```

Singleton middleware не должен принимать scoped services в конструкторе.

Singleton middleware также не должен принимать `ITelegramCurrentUpdateAccessor` или `IUpdateContextAccessor`. Current-update accessors scoped на один update.

Не регистрируй middleware напрямую как `IUpdateMiddleware` в `IServiceCollection`. TeleFlow строит middleware pipeline из middleware-регистраций, а не из произвольных `IUpdateMiddleware` services, и startup приложения намеренно падает с понятной ошибкой, если находит прямые регистрации.

Если middleware не вызывает `next(context)`, TeleFlow прекращает обработку этого update. Routing и handlers не выполнятся. Используй это для глобальных gates: ban list, tenant shutdown или жёсткие rate limits.

Если check относится к одному handler или группе handlers, лучше используй filter. Если logic относится к конкретному user workflow, держи её в handler service.

## Rate limiting

TeleFlow exposes `IUpdateRateLimiter` и default registration helpers:

```csharp
builder.Services.AddDefaultUpdateRateLimiting();
builder.Services.AddUpdateRateLimiter<MyRateLimiter>();
```

`AddDefaultUpdateRateLimiting()` добавляет rate-limit middleware. Он не добавляет скрытый no-op limiter. Если limiter-ы не зарегистрированы, middleware просто пропускает update дальше.

Custom limiter возвращает явное решение:

```csharp
public sealed class MyRateLimiter : IUpdateRateLimiter
{
    public ValueTask<UpdateRateLimitDecision> CheckAsync(
        UpdateContext context,
        CancellationToken cancellationToken = default)
    {
        if (ShouldReject(context))
        {
            return ValueTask.FromResult(
                UpdateRateLimitDecision.Rejected(
                    retryAfter: TimeSpan.FromSeconds(15),
                    policyName: "per-user-command"));
        }

        return ValueTask.FromResult(UpdateRateLimitDecision.Accepted);
    }
}
```

Используй `Rejected` для обычного throttling. Не кидай exceptions для ожидаемого rate-limit решения. Exceptions из limiter-а считаются настоящими failures и идут через обычный error path.

Rate-limit warning log содержит безопасные metadata: payload type, limiter type, retry-after и developer-controlled policy name. Он не логирует arbitrary limiter keys, message text, callback data или user-provided values.

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
