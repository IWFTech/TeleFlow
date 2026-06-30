# Architecture notes

TeleFlow имеет намеренно простую dependency direction.

```text
Application
  -> TeleFlow.Hosting when using Microsoft.Extensions.Hosting
      -> TeleFlow.Core
  -> TeleFlow.Telegram.Framework.LongPolling or Webhooks
      -> TeleFlow.Telegram.Framework
          -> TeleFlow.Telegram.Client
          -> TeleFlow.Telegram.Schema
          -> TeleFlow.Core
  -> TeleFlow.Storage.Memory or custom storage
  -> TeleFlow.Generators at build time
```

`TeleFlow.Core` не зависит от Telegram packages.

## Владение пакетами

- `TeleFlow.Core`: application, middleware, update processing, state contracts, replacement points.
- `TeleFlow.Hosting`: Microsoft.Extensions.Hosting adapter, который запускает настроенное TeleFlow application как `IHostedService`.
- `TeleFlow.Annotations`: compile-time metadata attributes. Файлы сгруппированы по ответственности, но все public annotation types остаются в стабильном namespace `TeleFlow.Annotations`.
- `IWF.TeleFlow.Generators`: source generator and analyzer package.
- `TeleFlow.Telegram.Schema`: generated Telegram DTOs and method models.
- `TeleFlow.Telegram.Client`: low-level Telegram client и generated client method extensions.
- `TeleFlow.Telegram.Framework`: Telegram handler runtime.
- `TeleFlow.Telegram.Framework.LongPolling`: framework transport adapter.
- `TeleFlow.Telegram.Framework.Webhooks`: framework transport adapter для ASP.NET Core.
- `TeleFlow.Telegram.LongPolling`: raw polling client.
- `TeleFlow.Telegram.Webhooks`: raw ASP.NET Core webhook endpoint helpers.
- `TeleFlow.Storage.Memory`: in-memory state provider.

## Runtime path

Framework runtime path:

```text
IUpdateSource
  -> IUpdateProcessor
      -> middleware
          -> Telegram dispatcher
              -> selected handler
                  -> Telegram client or application services
```

Generated registration влияет на metadata registration, а не на logical runtime path.

## Extension points

Replacement APIs используй только когда у replacement есть реальный owner:

- `IUpdateSource`
- `IUpdateDispatcher`
- `IUpdateMiddleware`
- `IUpdateRateLimiter`
- `IStateStore`
- `IStateDataStore`
- `IStateHistoryStore`
- `IStateKeyFactory`
- `ICallbackDataSerializer`
- `ITelegramClient`
- `ITelegramTransport`
- `ITelegramRequestExecutor`

Не создавай custom implementation просто потому, что interface существует.

## Ожидания при отладке

Production framework должен быть debuggable:

- startup errors должны указывать missing registration;
- handler registration mode должен быть виден в коде;
- routing должен быть представлен attributes;
- Telegram calls должны оставаться visible через `ctx.Bot`;
- state должен быть inspectable через storage keys;
- logs должны содержать достаточно context, чтобы связать update, handler и failure.
