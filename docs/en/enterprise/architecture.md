# Architecture Notes

TeleFlow has a deliberately simple dependency direction.

```text
Application
  -> TeleFlow.Framework.Hosting when using Microsoft.Extensions.Hosting
      -> TeleFlow.Framework.Core
  -> TeleFlow.Framework.LongPolling or Webhooks
      -> TeleFlow.Framework
          -> TeleFlow.Telegram.Client
          -> TeleFlow.Telegram.Schema
          -> TeleFlow.Framework.Core
  -> TeleFlow.Storage.Memory or custom storage
  -> TeleFlow.Generators at build time
```

`TeleFlow.Framework.Core` does not depend on Telegram packages.

## Package Ownership

- `TeleFlow.Framework.Core`: application, middleware, update processing, state contracts, replacement points.
- `TeleFlow.Framework.Hosting`: Microsoft.Extensions.Hosting adapter that runs a configured TeleFlow application as an `IHostedService`.
- `TeleFlow.Annotations`: compile-time metadata attributes. Files are grouped by responsibility, but every public annotation type stays in the stable `TeleFlow.Annotations` namespace.
- `IWF.TeleFlow.Generators`: source generator and analyzer package.
- `TeleFlow.Telegram.Schema`: generated Telegram DTOs and method models.
- `TeleFlow.Telegram.Client`: low-level Telegram client and generated client method extensions.
- `TeleFlow.Framework`: Telegram handler runtime.
- `TeleFlow.Framework.LongPolling`: framework transport adapter.
- `TeleFlow.Framework.Webhooks`: framework transport adapter for ASP.NET Core.
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
