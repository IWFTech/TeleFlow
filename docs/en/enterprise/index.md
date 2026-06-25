# Enterprise Guide

Enterprise adoption is not about adding more abstractions. It is about predictable behavior, clear ownership, testability, and operational failure modes.

TeleFlow is designed to be acceptable in serious .NET services because it keeps the important parts explicit:

- transport is selected directly;
- handler registration mode is visible;
- generated registration fails when metadata is missing;
- Telegram client remains directly available;
- core state and storage contracts are replaceable;
- framework internals use normal DI and logging.

## Recommended Enterprise Defaults

Use:

```csharp
builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
```

And exactly one transport:

```csharp
builder.Services.AddLongPolling();
```

Or:

```csharp
builder.Services.AddWebhook(options => options.Path = "/telegram/webhook");
```

Use generated registration as the default. Use reflection registration only when it is written down as an intentional decision.

## Boundaries

Keep Telegram-specific code near the bot layer:

```text
Bot
  Handlers
  Filters
  Scenes
  Telegram-specific DTO mapping
Application
  Use cases
  Services
Domain
  Business models
Infrastructure
  Storage
  External clients
```

Do not let Telegram DTOs leak everywhere unless the whole product is Telegram-only and that is an intentional tradeoff.

## State Policy

Memory state storage is not an enterprise storage strategy. It is a development and single-process option.

For production:

- choose durable storage;
- define key structure;
- decide concurrency semantics;
- decide TTL and cleanup;
- test wizard history;
- test recovery after process restart.

## Error Policy

Use error handlers for known business failures. Let unknown failures remain visible.

Recommended:

- handler-level error handlers for validation and expected user errors;
- structured logs for unknown exceptions;
- host-level restart policy;
- alerting on repeated failures;
- tests for handled error flows.

## Performance Policy

Do not assume the framework is the bottleneck. Measure:

- handler time;
- Telegram API request count;
- Telegram API request latency;
- storage latency;
- queue or webhook latency if present.

TeleFlow already avoids the most obvious enterprise objection: the recommended assembly registration path does not rely on silent runtime reflection.

See [Performance and scaling](performance.md) for measurement and scaling guidance.

## Upgrade Policy

Keep package updates controlled:

- pin package versions;
- run tests on dependency updates;
- test generated registration after generator updates;
- track Telegram Bot API schema updates separately from application behavior;
- keep release notes.

See [Versioning and releases](versioning.md) for the release policy.

## What To Document In Your Project

Every production bot should document:

- transport choice;
- registration mode;
- storage backend;
- state key policy;
- callback payload policy;
- error handling policy;
- deployment topology;
- how to replay or inspect failed updates;
- who owns bot token rotation.
