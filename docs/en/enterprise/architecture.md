# Architecture Notes

TeleFlow has a deliberately simple dependency direction.

```text
Application
  -> TeleFlow.Telegram.Framework.LongPolling or Webhooks
      -> TeleFlow.Telegram.Framework
          -> TeleFlow.Telegram.Client
          -> TeleFlow.Telegram.Schema
          -> TeleFlow.Core
  -> TeleFlow.Storage.Memory or custom storage
  -> TeleFlow.Generators at build time
```

`TeleFlow.Core` does not depend on Telegram packages.

## Package Ownership

- `TeleFlow.Core`: application, middleware, update processing, state contracts, replacement points.
- `TeleFlow.Annotations`: compile-time metadata attributes.
- `IWF.TeleFlow.Generators`: source generator and analyzer package.
- `TeleFlow.Telegram.Schema`: generated Telegram DTOs and method models.
- `TeleFlow.Telegram.Client`: low-level Telegram client and generated client method extensions.
- `TeleFlow.Telegram.Framework`: Telegram handler runtime.
- `TeleFlow.Telegram.Framework.LongPolling`: framework transport adapter.
- `TeleFlow.Telegram.Framework.Webhooks`: framework transport adapter for ASP.NET Core.
- `TeleFlow.Telegram.LongPolling`: raw polling client.
- `TeleFlow.Telegram.Webhooks`: raw ASP.NET Core webhook endpoint helpers.
- `TeleFlow.Storage.Memory`: in-memory state provider.

## Runtime Path

Framework runtime path:

```text
IUpdateSource
  -> IUpdateProcessor
      -> middleware
          -> Telegram dispatcher
              -> selected handler
                  -> Telegram client or application services
```

Generated registration affects metadata registration, not the logical runtime path.

## Extension Points

Use replacement APIs only when there is a real owner for the replacement:

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

Avoid creating a custom implementation just because the interface exists.

## Debugging Expectations

A production framework should be debuggable:

- startup errors should identify missing registration;
- handler registration mode should be visible in code;
- routing should be represented by attributes;
- Telegram calls should remain visible through `ctx.Bot`;
- state should be inspectable through storage keys;
- logs should include enough context to connect update, handler, and failure.
