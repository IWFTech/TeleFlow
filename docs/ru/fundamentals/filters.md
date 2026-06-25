# Фильтры

Filters решают, может ли handler выполниться для текущего update.

В TeleFlow есть два типа filters:

- built-in filters через attributes;
- custom filters через `ITelegramFilter<TContext>`.

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
