# Filters

Filters decide whether a handler is allowed to run for the current update.

TeleFlow has two kinds of filters:

- built-in filters represented by attributes;
- custom filters implemented through `ITelegramFilter<TContext>`.

## Built-In Filters

Common examples:

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

Built-in filters are metadata. The framework evaluates them during handler selection.

## Custom Filters

Create a filter:

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

Use it on a handler:

```csharp
[Command("support")]
[UseFilter<BusinessHoursFilter>]
public Task Support(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Support is online.", ct);
}
```

Register dependencies normally:

```csharp
builder.Services.AddSingleton<BusinessHoursFilter>();
```

## Filter Scope

Use filters for decisions that are about matching:

- chat type;
- sender id;
- Telegram member role;
- update content;
- business rules that decide whether this handler should run.

Do not use filters for side effects. If the handler should always run and then decide what to do, put that logic inside the handler or an application service.

## Recommended Style

Prefer built-in attributes for simple Telegram conditions. Use custom filters when the condition needs services, time, storage, or application rules.

Keep filters small. A filter that writes to a database, sends messages, and changes state is probably doing handler work.
