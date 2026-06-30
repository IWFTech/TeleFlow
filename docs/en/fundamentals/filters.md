# Filters

Filters decide whether a handler is allowed to run for the current update.

TeleFlow has two kinds of filters:

- built-in filters represented by attributes;
- custom filters implemented through `ITelegramFilter<TContext>`;
- parameterized custom filter attributes implemented through `TelegramFilterAttribute<TFilter>` and `ITelegramFilter<TContext, TAttribute>`.

## Attributes And Filters

In C#, both routes and filters are written as attributes on a handler method, but they do not have the same role.

Route attributes decide what kind of update the handler represents and which route values should be extracted:

```csharp
[Command("start")]
[CommandTemplate("ban {userId:int}")]
[Callback<TicketAction>]
```

Filter attributes add conditions that must be true before that handler can run:

```csharp
[HasPhoto]
[ChatType(TelegramChatType.Private)]
[UseFilter<AdminOnlyFilter>]
[RequireFeature("billing")]
```

Filters are not routes. A handler that uses filters or state constraints still needs an explicit route attribute such as `[Message]`, `[Command]`, `[Callback]`, or `[ChatMemberUpdated]`.

For example:

```csharp
[CommandTemplate("ban {userId:int}")]
[UseFilter<AdminOnlyFilter>]
public Task Ban(MessageContext ctx, int userId, CancellationToken ct)
{
    return ctx.Message.AnswerAsync($"Banning {userId}.", ct);
}
```

`[CommandTemplate]` matches the command and binds `userId`. `AdminOnlyFilter` decides whether the current user is allowed to call the handler.

Keep command parsing in routes. Use filters for permissions, anti-spam, feature flags, storage-backed checks, and other yes/no decisions. A filter that parses command arguments is usually moving routing logic into the wrong layer.

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

Use `[UseFilter<TFilter>]` when the filter does not need per-handler metadata.

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

## Parameterized Custom Filter Attributes

Use a parameterized custom filter attribute when the same filter logic needs different metadata on different handlers.

Define the attribute:

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

Implement the typed filter:

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

Use it on a handler:

```csharp
[Command("billing")]
[RequireFeature("billing", AllowPreviewUsers = true)]
public Task Billing(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Billing is enabled.", ct);
}
```

Register the filter type in dependency injection:

```csharp
builder.Services.AddScoped<RequireFeatureFilter>();
```

Parameterized custom filter attributes support constructor arguments and named arguments that are valid C# attribute constants. Generic custom filter attribute classes are intentionally not supported in v1.

The generated registration path emits the attribute metadata at build time. During update processing, TeleFlow uses a prepared call site; it does not rediscover attributes or invoke filters through `dynamic`.

## Filter Scope

Use filters for decisions that are about matching:

- chat type;
- sender id;
- Telegram member role;
- update content;
- business rules that decide whether this handler should run.

Do not use filters for side effects. If the handler should always run and then decide what to do, put that logic inside the handler or an application service.

Filters should return a decision. They are not decorators, middleware, transactions, retries, metrics hooks, or AOP.

## Recommended Style

Prefer built-in attributes for simple Telegram conditions. Use custom filters when the condition needs services, time, storage, or application rules.

Keep filters small. A filter that writes to a database, sends messages, and changes state is probably doing handler work.
