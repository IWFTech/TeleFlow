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

## Webhook Returns Unauthorized

Check `SecretToken` configuration and Telegram webhook settings. The incoming request must use the expected secret token.

## Bot Gets Old Updates After Restart

Telegram can return pending updates after downtime. Current public API does not document a drop-pending-updates option. Design deployment and startup behavior with pending updates in mind.
