# Troubleshooting

## `AddTelegramBot must be called before ...`

Framework handler and transport APIs require Telegram bot services.

Correct order:

```csharp
builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();
```

## `Assembly does not contain generated Telegram handler metadata`

`AddTelegramHandlersFromAssembly(...)` requires `IWF.TeleFlow.Generators`.

Fix:

```xml
<PackageReference Include="IWF.TeleFlow.Generators" Version="..." PrivateAssets="all" />
```

Then rebuild the application.

If you intentionally do not want generated registration, use:

```csharp
builder.Services.AddTelegramHandler<StartHandler>();
```

Or register a module explicitly:

```csharp
builder.Services.AddTelegramModule<AdminHandlers>();
```

Do not switch to deprecated reflection assembly registration to fix missing generated metadata.

## `TLF027`: handler is missing a route attribute

State and filter attributes constrain a handler, but they do not route updates by themselves.

Wrong:

```csharp
[State("registration:name")]
[HasText]
public Task Name(MessageContext ctx, CancellationToken ct)
{
    return Task.CompletedTask;
}
```

Correct:

```csharp
[Message]
[State("registration:name")]
[HasText]
public Task Name(MessageContext ctx, CancellationToken ct)
{
    return Task.CompletedTask;
}
```

Use `[Message]`, `[Command]`, `[Callback]`, `[ChatMemberUpdated]`, or another explicit route attribute before adding state and filter constraints.

## `Handler dependency was not registered`

TeleFlow validates handler method parameters before normal update processing starts. If a handler asks for a service, that service must be registered in DI:

```csharp
public sealed class TicketHandler
{
    [CommandTemplate("ticket {id:long}")]
    public Task Ticket(
        MessageContext ctx,
        ITicketRepository tickets,
        CancellationToken ct)
    {
        // ...
    }
}
```

Register the dependency before building the app:

```csharp
builder.Services.AddScoped<ITicketRepository, EfTicketRepository>();
```

The same rule applies to Telegram error handlers and custom filters.

## Current Update Accessor Fails Outside Update Processing

`ITelegramCurrentUpdateAccessor` is scoped to one incoming Telegram update. It works inside handlers, middleware, and scoped services reached from the update pipeline.

It does not work from application startup, background jobs, singleton services, or code that runs before an update exists.

Use it from scoped services:

```csharp
public sealed class UserService(ITelegramCurrentUpdateAccessor current)
{
    public long RequireUserId()
    {
        return current.User?.Id
            ?? throw new InvalidOperationException("This operation requires a Telegram user.");
    }
}
```

Do not inject it into singleton services.

## Can I Read The Token From `appsettings.json`?

Yes. TeleFlow does not care where the token comes from. Read it through normal .NET configuration and pass the resolved value explicitly:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = configuration["Telegram:BotToken"]
        ?? throw new InvalidOperationException("Telegram:BotToken is not configured.");
});
```

See [Configuration and secrets](../getting-started/configuration.md).

## Handler Does Not Match

Check:

- update type: message, callback, chat member;
- command prefix;
- text exact match vs contains;
- state requirement;
- class-level filters;
- custom filter return value;
- allowed updates for long polling.

## State Is Not Available

Register state storage:

```csharp
builder.Services.AddMemoryStateStorage();
```

For custom storage, make sure `IStateStore` and state middleware are registered.

## Wizard Back Fails

Wizard back requires state history storage. `AddMemoryStateStorage()` registers it. Custom storage must provide `IStateHistoryStore`.

## Callback Data Is Too Long

Telegram callback data is limited to 64 UTF-8 bytes. Use compact payloads:

```csharp
[CallbackData("t")]
public sealed record TicketAction(long Id, string A);
```

Do not put large JSON payloads in callback data.

## Callback Data Failed To Deserialize

If logs contain `Telegram callback data failed to deserialize`, the callback
matched a typed `[Callback<TPayload>]` route by compact prefix and field count,
but the payload could not be decoded. Common causes:

- an old inline button was clicked after you changed the callback payload type;
- a numeric, boolean, or enum field contains an invalid value;
- another bot component produced callback data with the same prefix but a
  different field format.

TeleFlow treats that typed route as not matched, so a raw callback fallback can
answer the user. Keep callback payloads versionable: use short stable prefixes,
prefer IDs over large objects, and create a new prefix when a deployed callback
shape changes incompatibly.

## Webhook Returns Unauthorized

Check `SecretToken` configuration and Telegram webhook settings. The incoming request must use the expected secret token.

## Bot Gets Old Updates After Restart

Telegram can return pending updates after downtime. Current public API does not document a drop-pending-updates option. Design deployment and startup behavior with pending updates in mind.
