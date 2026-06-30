# Troubleshooting

## `AddTelegramBot must be called before ...`

Framework handler и transport APIs требуют Telegram bot services.

Правильный order:

```csharp
builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();
```

## `Assembly does not contain generated Telegram handler metadata`

`AddTelegramHandlersFromAssembly(...)` требует `IWF.TeleFlow.Generators`.

Исправление:

```xml
<PackageReference Include="IWF.TeleFlow.Generators" Version="..." PrivateAssets="all" />
```

Потом пересобери application.

Если generated registration не нужен, используй:

```csharp
builder.Services.AddTelegramHandler<StartHandler>();
```

Или зарегистрируй module явно:

```csharp
builder.Services.AddTelegramModule<AdminHandlers>();
```

Не переходи на deprecated reflection assembly registration как способ починить missing generated metadata.

## `TLF027`: у handler нет route attribute

State и filter attributes ограничивают handler, но сами не route-ят updates.

Неправильно:

```csharp
[State("registration:name")]
[HasText]
public Task Name(MessageContext ctx, CancellationToken ct)
{
    return Task.CompletedTask;
}
```

Правильно:

```csharp
[Message]
[State("registration:name")]
[HasText]
public Task Name(MessageContext ctx, CancellationToken ct)
{
    return Task.CompletedTask;
}
```

Используй `[Message]`, `[Command]`, `[Callback]`, `[ChatMemberUpdated]` или другой явный route attribute перед state и filter constraints.

## Можно ли читать token из `appsettings.json`?

Да. TeleFlow не важно, откуда пришёл token. Прочитай его через обычную .NET configuration и явно передай resolved value:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = configuration["Telegram:BotToken"]
        ?? throw new InvalidOperationException("Telegram:BotToken is not configured.");
});
```

Смотри [Конфигурация и секреты](../getting-started/configuration.md).

## Handler не срабатывает

Проверь:

- update type: message, callback, chat member;
- command prefix;
- text exact match vs contains;
- state requirement;
- class-level filters;
- custom filter return value;
- allowed updates for long polling.

## State недоступен

Зарегистрируй state storage:

```csharp
builder.Services.AddMemoryStateStorage();
```

Для custom storage убедись, что `IStateStore` и state middleware registered.

## Wizard back не работает

Wizard back требует state history storage. `AddMemoryStateStorage()` его регистрирует. Custom storage должен предоставить `IStateHistoryStore`.

## Callback data слишком длинная

Telegram callback data ограничен 64 UTF-8 bytes. Используй compact payloads:

```csharp
[CallbackData("t")]
public sealed record TicketAction(long Id, string A);
```

Не клади large JSON payloads в callback data.

## Webhook возвращает unauthorized

Проверь `SecretToken` configuration и Telegram webhook settings. Incoming request должен использовать expected secret token.

## Бот получает старые updates после рестарта

Telegram может вернуть pending updates после downtime. Current public API не документирует drop-pending-updates option. Deployment и startup behavior нужно проектировать с учётом pending updates.
