# Long polling

Long polling - самый простой способ запустить Telegram-бота. Приложение повторно вызывает Telegram `getUpdates` и обрабатывает полученные updates.

## Framework long polling

Установка:

```bash
dotnet add package IWF.TeleFlow.Framework.LongPolling --prerelease
```

Регистрация:

```csharp
builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();
```

Настройка:

```csharp
builder.Services.AddLongPolling(options =>
{
    options.TimeoutSeconds = 30;
    options.Limit = 100;
    options.AllowedUpdates = TelegramAllowedUpdates.Auto;
    options.Backoff.MinDelay = TimeSpan.FromSeconds(1);
    options.Backoff.MaxDelay = TimeSpan.FromSeconds(5);
});
```

`TelegramAllowedUpdates.Auto` определяет allowed update types по registered handlers. Это позволяет не поддерживать update type strings руками на старте.

Режимы allowed updates:

```csharp
options.AllowedUpdates = TelegramAllowedUpdates.Auto;
options.AllowedUpdates = TelegramAllowedUpdates.All;
options.AllowedUpdates = TelegramAllowedUpdates.Only(
    TelegramUpdateType.Message,
    TelegramUpdateType.CallbackQuery);
```

Long polling retry-ит transient `getUpdates` failures с настраиваемым backoff. Если Telegram client отдаёт `TelegramRetryAfterException`, polling ждёт Telegram-provided retry delay вместо обычной backoff delay. Handler failures не swallowing. Offset продвигается только после успешной обработки update.

## Когда использовать long polling

Long polling подходит, когда:

- ты разрабатываешь локально;
- запускаешь небольшой worker;
- deployment environment не имеет inbound public HTTP;
- простота инфраструктуры важнее webhook push delivery.

## Операционные заметки

Long polling applications - это long-running processes. Запускай их под host, который перезапускает process on failure и собирает logs.

Для production:

- передавай cancellation от host;
- по возможности делай handlers idempotent;
- избегай process-local state для multi-instance deployments;
- предпочитай один active long polling worker на bot token, если специально не строишь координацию.

## Drop pending updates

TeleFlow long polling читает updates из Telegram по polling offset, которым управляет polling client. Если бот был offline, Telegram может вернуть pending updates. Startup behavior для production bots нужно проектировать осознанно.

Текущая public docs не заявляет `drop_pending_updates` option. Если такой API появится позже, его нужно описать здесь с точными semantics.
