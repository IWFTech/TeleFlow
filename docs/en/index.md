# TeleFlow Documentation

TeleFlow is a Telegram bot framework for .NET that keeps the first handler simple and the grown application explicit.

The core idea is direct: your bot should not become a black box just because the framework is convenient. TeleFlow keeps handler code close to normal C#, uses normal dependency injection, exposes the Telegram client directly, and moves handler metadata generation to build time on the recommended path.

## Start Here

- [Quickstart](getting-started/quickstart.md): build a minimal long polling bot.
- [Configuration and secrets](getting-started/configuration.md): read token and webhook settings through normal .NET configuration.
- [Recommended paths](getting-started/recommended-paths.md): choose the right learning path for your level.
- [Package guide](getting-started/packages.md): understand what to install and why.
- [Support desk tutorial](tutorials/support-desk.md): a realistic bot with DI, state, callbacks, and admin actions.
- [Roadmap](roadmap.md): planned framework work that is not part of the current public API yet.
- [Russian documentation](../ru/index.md): same documentation in Russian.

## Learning Paths

### First Bot

For trainees and juniors who want to get something working without being buried in infrastructure:

1. [Quickstart](getting-started/quickstart.md)
2. [Configuration and secrets](getting-started/configuration.md)
3. [Handlers and routing](fundamentals/handlers-and-routing.md)
4. [Callbacks and keyboards](features/callbacks-and-keyboards.md)
5. [State and wizard](features/state-and-wizard.md)
6. [Support desk tutorial](tutorials/support-desk.md)

### Production Bot

For developers who already understand .NET services and want correct project shape:

1. [Application model](fundamentals/application-model.md)
2. [Configuration and secrets](getting-started/configuration.md)
3. [Project structure](fundamentals/project-structure.md)
4. [Dependency injection](fundamentals/dependency-injection.md)
5. [Telegram client and schema](features/telegram-client.md)
6. [Errors and diagnostics](features/errors-and-diagnostics.md)
7. [Testing](advanced/testing.md)

### Enterprise Bot

For teams that care about predictable runtime behavior, ownership boundaries, deployment, and future maintenance:

1. [Enterprise guide](enterprise/index.md)
2. [Deployment](enterprise/deployment.md)
3. [Performance and scaling](enterprise/performance.md)
4. [Versioning and releases](enterprise/versioning.md)
5. [Production checklist](enterprise/production-checklist.md)
6. [Architecture notes](enterprise/architecture.md)
7. [Generated registration](advanced/generated-registration.md)
8. [Custom storage](advanced/custom-storage.md)

## Documentation Map

### Getting Started

- [Quickstart](getting-started/quickstart.md)
- [Configuration and secrets](getting-started/configuration.md)
- [Recommended paths](getting-started/recommended-paths.md)
- [Packages](getting-started/packages.md)

### Tutorials

- [Support desk bot](tutorials/support-desk.md)

### Fundamentals

- [Application model](fundamentals/application-model.md)
- [Project structure](fundamentals/project-structure.md)
- [Handlers and routing](fundamentals/handlers-and-routing.md)
- [Filters](fundamentals/filters.md)
- [Dependency injection](fundamentals/dependency-injection.md)
- [Cancellation](fundamentals/cancellation.md)

### Features

- [Telegram client and schema](features/telegram-client.md)
- [Callbacks and keyboards](features/callbacks-and-keyboards.md)
- [State and wizard](features/state-and-wizard.md)
- [Roles and chat members](features/roles-and-chat-members.md)
- [Errors and diagnostics](features/errors-and-diagnostics.md)

### Transports

- [Long polling](transports/long-polling.md)
- [Webhooks](transports/webhooks.md)
- [Raw transports](transports/raw-transports.md)

### Advanced

- [Generated registration](advanced/generated-registration.md)
- [Custom storage](advanced/custom-storage.md)
- [Middleware and rate limiting](advanced/middleware-and-rate-limiting.md)
- [Testing](advanced/testing.md)

### Enterprise

- [Enterprise guide](enterprise/index.md)
- [Deployment](enterprise/deployment.md)
- [Performance and scaling](enterprise/performance.md)
- [Versioning and releases](enterprise/versioning.md)
- [Architecture notes](enterprise/architecture.md)
- [Production checklist](enterprise/production-checklist.md)

### Reference

- [Attributes](reference/attributes.md)
- [Options and extension points](reference/options-and-extension-points.md)
- [Troubleshooting](reference/troubleshooting.md)

### Planning

- [Roadmap](roadmap.md)

## Philosophy

TeleFlow exists for the moment when a simple Telegram bot becomes an application.

It should remain easy to start:

- one console app;
- one command handler;
- one token;
- one long polling transport.

It should also remain honest when the project grows:

- no hidden reflection fallback on the recommended registration path;
- no requirement to wrap every Telegram call in a framework abstraction;
- no special service container;
- no fake enterprise story based only on naming.

The design preference is simple: explicit behavior, predictable runtime, small public contracts, and extension points only where replacement is a real use case.

## Current Scope

The current repository includes:

- Telegram Bot API client and generated schema models;
- generated client method extension methods;
- handler framework for messages, callbacks, and chat member updates;
- command, text, template, regex, media, callback, state, scene, role, and custom filter attributes;
- typed callback payloads through `[CallbackData]` and `[Callback<TPayload>]`;
- inline keyboards, reply keyboards, keyboard removal, force reply, and chat actions;
- state, state data, wizard navigation, and memory storage;
- long polling and ASP.NET Core webhook adapters;
- raw long polling and raw webhook packages;
- generated handler registration and explicit direct registration;
- middleware, update processor, update source, dispatcher, storage, callback serializer, and client replacement points.
