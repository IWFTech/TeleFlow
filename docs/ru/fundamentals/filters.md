# Фильтры

Filters решают, может ли handler выполниться для текущего update.

В TeleFlow есть два типа filters:

- built-in filters через attributes;
- custom filters через `ITelegramFilter<TContext>`.

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

## Scope фильтра

Используй filters для решений о matching:

- chat type;
- sender id;
- Telegram member role;
- update content;
- business rules, которые решают, должен ли этот handler выполниться.

Не используй filters для side effects. Если handler должен всегда запускаться и уже внутри решать, что делать, оставь логику в handler или application service.

## Рекомендуемый стиль

Для простых Telegram conditions используй built-in attributes. Custom filters нужны, когда условию требуются services, time, storage или application rules.

Filter должен быть маленьким. Filter, который пишет в database, отправляет messages и меняет state, скорее всего делает работу handler.
