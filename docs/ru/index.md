# Документация TeleFlow

TeleFlow - это фреймворк для Telegram-ботов на .NET, который оставляет первый handler простым, а выросшее приложение явным и понятным.

Главная идея простая: бот не должен превращаться в чёрный ящик только потому, что framework удобный. TeleFlow оставляет handler code близким к обычному C#, использует обычный dependency injection, даёт прямой доступ к Telegram client и переносит handler metadata generation в build time на рекомендуемом пути.

## С чего начать

- [Quickstart](getting-started/quickstart.md): минимальный long polling bot.
- [Конфигурация и секреты](getting-started/configuration.md): token и webhook settings через обычную .NET configuration.
- [Recommended paths](getting-started/recommended-paths.md): какой путь выбрать под твой уровень.
- [Package guide](getting-started/packages.md): что ставить и почему.
- [Support desk tutorial](tutorials/support-desk.md): реалистичный бот с DI, state, callbacks и admin actions.
- [Roadmap](roadmap.md): запланированная работа, которая ещё не является текущим public API.
- [English documentation](../en/index.md): эта же документация на английском.

## Пути изучения

### Первый бот

Для трейни и джунов, которым нужно быстро получить рабочий результат без перегруза инфраструктурой:

1. [Quickstart](getting-started/quickstart.md)
2. [Конфигурация и секреты](getting-started/configuration.md)
3. [Handlers and routing](fundamentals/handlers-and-routing.md)
4. [Callbacks and keyboards](features/callbacks-and-keyboards.md)
5. [State and wizard](features/state-and-wizard.md)
6. [Support desk tutorial](tutorials/support-desk.md)

### Production bot

Для разработчиков, которые уже понимают .NET services и хотят правильную структуру:

1. [Application model](fundamentals/application-model.md)
2. [Конфигурация и секреты](getting-started/configuration.md)
3. [Структура проекта](fundamentals/project-structure.md)
4. [Dependency injection](fundamentals/dependency-injection.md)
5. [Telegram client and schema](features/telegram-client.md)
6. [Errors and diagnostics](features/errors-and-diagnostics.md)
7. [Testing](advanced/testing.md)

### Enterprise bot

Для команд, которым важны предсказуемый runtime, границы владения, deployment и поддержка через год:

1. [Enterprise guide](enterprise/index.md)
2. [Deployment](enterprise/deployment.md)
3. [Performance и scaling](enterprise/performance.md)
4. [Версионирование и релизы](enterprise/versioning.md)
5. [Production checklist](enterprise/production-checklist.md)
6. [Architecture notes](enterprise/architecture.md)
7. [Generated registration](advanced/generated-registration.md)
8. [Custom storage](advanced/custom-storage.md)

## Карта документации

### Getting Started

- [Quickstart](getting-started/quickstart.md)
- [Конфигурация и секреты](getting-started/configuration.md)
- [Recommended paths](getting-started/recommended-paths.md)
- [Packages](getting-started/packages.md)

### Tutorials

- [Support desk bot](tutorials/support-desk.md)

### Fundamentals

- [Application model](fundamentals/application-model.md)
- [Структура проекта](fundamentals/project-structure.md)
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
- [Performance и scaling](enterprise/performance.md)
- [Версионирование и релизы](enterprise/versioning.md)
- [Architecture notes](enterprise/architecture.md)
- [Production checklist](enterprise/production-checklist.md)

### Reference

- [Attributes](reference/attributes.md)
- [Options and extension points](reference/options-and-extension-points.md)
- [Troubleshooting](reference/troubleshooting.md)

### Planning

- [Roadmap](roadmap.md)

## Философия

TeleFlow создан для момента, когда простой Telegram-бот становится приложением.

Он должен быть простым на старте:

- один console app;
- один command handler;
- один token;
- один long polling transport.

И он должен оставаться честным, когда проект растёт:

- без скрытого reflection fallback на рекомендуемом пути регистрации;
- без требования заворачивать каждый Telegram call в framework-абстракцию;
- без специального service container;
- без fake enterprise story, которая держится только на названиях.

Предпочтение в дизайне простое: явное поведение, предсказуемый runtime, маленькие public contracts и extension points только там, где замена реально нужна.

## Текущий scope

В текущем репозитории есть:

- Telegram Bot API client и generated schema models;
- generated client method extension methods;
- handler framework для messages, callbacks и chat member updates;
- атрибуты для command, text, template, regex, media, callback, state, scene, role и custom filters;
- typed callback payloads через `[CallbackData]` и `[Callback<TPayload>]`;
- inline keyboards, reply keyboards, keyboard removal, force reply и chat actions;
- state, state data, wizard navigation и memory storage;
- long polling и ASP.NET Core webhook adapters;
- raw long polling и raw webhook packages;
- generated handler registration и explicit direct registration;
- replacement points для middleware, update processor, update source, dispatcher, storage, callback serializer и client.
