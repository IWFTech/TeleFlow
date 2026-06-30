# Фильтры

Filters решают, может ли handler выполниться для текущего update.

В TeleFlow есть два типа filters:

- built-in filters через attributes;
- custom filters через `ITelegramFilter<TContext>`;
- parameterized custom filter attributes через `TelegramFilterAttribute<TFilter>` и `ITelegramFilter<TContext, TAttribute>`.

## Attributes и filters

В C# и routes, и filters записываются как attributes над handler method, но роль у них разная.

Route attributes решают, какой update подходит handler-у и какие route values нужно достать:

```csharp
[Command("start")]
[CommandTemplate("ban {userId:int}")]
[Callback<TicketAction>]
```

Filter attributes добавляют условия, которые должны быть выполнены перед вызовом handler:

```csharp
[HasPhoto]
[ChatType(TelegramChatType.Private)]
[UseFilter<AdminOnlyFilter>]
[RequireFeature("billing")]
```

Filters не являются routes. Handler с filters или state constraints всё равно должен иметь явный route attribute: `[Message]`, `[Command]`, `[Callback]` или `[ChatMemberUpdated]`.

Например:

```csharp
[CommandTemplate("ban {userId:int}")]
[UseFilter<AdminOnlyFilter>]
public Task Ban(MessageContext ctx, int userId, CancellationToken ct)
{
    return ctx.Message.AnswerAsync($"Banning {userId}.", ct);
}
```

`[CommandTemplate]` матчится на команду и bind-ит `userId`. `AdminOnlyFilter` решает, можно ли текущему user вызвать этот handler.

Парсинг команды держи в routes. Filters используй для permissions, anti-spam, feature flags, checks через storage и других yes/no решений. Filter, который парсит command arguments, скорее всего переносит routing logic не в тот слой.

## Built-in filters

Примеры:

```csharp
[Command("start")]
[ChatType(TelegramChatType.Private)]
public Task Start(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Private start.", ct);
}
```

```csharp
[Message]
[HasText]
[FromUser(123456789)]
public Task FromKnownUser(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Known user.", ct);
}
```

Built-in filters - это metadata. Framework оценивает их во время handler selection.

## Custom filters

Используй `[UseFilter<TFilter>]`, когда filter не нуждается в metadata на конкретном handler.

Создай filter:

```csharp
public sealed class BusinessHoursFilter : ITelegramFilter<MessageContext>
{
    private readonly TimeProvider _timeProvider;

    public BusinessHoursFilter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public ValueTask<bool> MatchesAsync(
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetLocalNow();
        var allowed = now.Hour is >= 9 and < 18;
        return ValueTask.FromResult(allowed);
    }
}
```

Используй его на handler:

```csharp
[Command("support")]
[UseFilter<BusinessHoursFilter>]
public Task Support(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Support is online.", ct);
}
```

Регистрируй dependencies обычным способом:

```csharp
builder.Services.AddSingleton<BusinessHoursFilter>();
```

## Parameterized custom filter attributes

Используй parameterized custom filter attribute, когда одна и та же filter logic должна получать разные metadata на разных handlers.

Определи attribute:

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireFeatureAttribute : TelegramFilterAttribute<RequireFeatureFilter>
{
    public RequireFeatureAttribute(string feature)
    {
        Feature = feature;
    }

    public string Feature { get; }

    public bool AllowPreviewUsers { get; set; }
}
```

Реализуй typed filter:

```csharp
public sealed class RequireFeatureFilter
    : ITelegramFilter<MessageContext, RequireFeatureAttribute>
{
    private readonly IFeatureAccess _features;

    public RequireFeatureFilter(IFeatureAccess features)
    {
        _features = features;
    }

    public ValueTask<bool> MatchesAsync(
        MessageContext context,
        RequireFeatureAttribute attribute,
        CancellationToken cancellationToken = default)
    {
        return _features.CanUseAsync(
            context.TelegramMessage.From?.Id,
            attribute.Feature,
            attribute.AllowPreviewUsers,
            cancellationToken);
    }
}
```

Используй на handler:

```csharp
[Command("billing")]
[RequireFeature("billing", AllowPreviewUsers = true)]
public Task Billing(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Billing is enabled.", ct);
}
```

Зарегистрируй filter type в DI:

```csharp
builder.Services.AddScoped<RequireFeatureFilter>();
```

Parameterized custom filter attributes поддерживают constructor arguments и named arguments, которые являются валидными C# attribute constants. Generic custom filter attribute classes в v1 намеренно не поддерживаются.

Generated registration path записывает attribute metadata на build time. Во время обработки update TeleFlow использует заранее подготовленный call site; framework не ищет attributes заново и не вызывает filters через `dynamic`.

## Scope фильтра

Используй filters для решений о matching:

- chat type;
- sender id;
- Telegram member role;
- update content;
- business rules, которые решают, должен ли этот handler выполниться.

Не используй filters для side effects. Если handler должен всегда запускаться и уже внутри решать, что делать, оставь логику в handler или application service.

Filter должен возвращать решение. Это не decorators, не middleware, не transactions, не retries, не metrics hooks и не AOP.

## Рекомендуемый стиль

Для простых Telegram conditions используй built-in attributes. Custom filters нужны, когда условию требуются services, time, storage или application rules.

Filter должен быть маленьким. Filter, который пишет в database, отправляет messages и меняет state, скорее всего делает работу handler.
