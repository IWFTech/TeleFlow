# Структура проекта

TeleFlow не навязывает одну application architecture. Бот может начаться как один console app и вырасти в service с понятными границами.

Используй минимальную структуру, в которой ownership остаётся очевидным.

## Маленький бот

Для маленького бота достаточно одного проекта:

```text
EchoBot/
  Program.cs
  Handlers/
    StartHandler.cs
    EchoHandler.cs
  appsettings.json
```

Этого достаточно, когда у бота несколько commands, нет сложной domain model, а process-local state acceptable.

Правила:

- один handler class на один use case;
- DI registrations держи в `Program.cs`;
- не вводи repositories, пока нет storage;
- не дели projects, пока коду не нужна настоящая boundary.

## Product bot

Для реального продукта держи bot layer явным, а business logic выноси из handlers:

```text
SupportBot/
  Program.cs
  Configuration/
    TelegramSettings.cs
  Handlers/
    StartHandler.cs
    TicketHandlers.cs
    AdminTicketHandlers.cs
  Scenes/
    TicketScene.cs
  Filters/
    BusinessHoursFilter.cs
  Callbacks/
    TicketAction.cs
  Application/
    Tickets/
      TicketService.cs
      ITicketRepository.cs
    Notifications/
      NotificationService.cs
  Domain/
    Ticket.cs
    TicketStatus.cs
  Infrastructure/
    Storage/
      InMemoryTicketRepository.cs
    Telegram/
      TelegramNotificationFormatter.cs
```

Рекомендуемый ownership:

| Folder | За что отвечает |
| --- | --- |
| `Handlers` | Telegram routing и user interaction. |
| `Scenes` | Conversation flows и wizard steps. |
| `Filters` | Telegram-specific handler filters. |
| `Callbacks` | Typed callback payload DTOs. |
| `Application` | Use cases и application services. |
| `Domain` | Product concepts, которые не должны зависеть от Telegram. |
| `Infrastructure` | Storage, external clients, adapters и technical implementations. |

Handlers должны orchestrate. Они не должны становиться местом, где живут все business rules.

## Большой бот

Для большого бота дели projects, когда это создаёт настоящую boundary:

```text
Company.Bot/
  Program.cs
  Handlers/
  Scenes/
  Filters/
  Callbacks/

Company.Application/
  Tickets/
  Users/
  Billing/

Company.Domain/
  Tickets/
  Users/
  Billing/

Company.Infrastructure/
  Persistence/
  ExternalServices/
  Telegram/
```

Dependency direction:

```text
Company.Bot -> Company.Application -> Company.Domain
Company.Infrastructure -> Company.Application
Company.Bot -> Company.Infrastructure
```

Telegram DTOs лучше держать в `Company.Bot` и `Company.Infrastructure.Telegram`. Если весь продукт Telegram-only, передавать Telegram ids через application services нормально. Передавать full Telegram schema objects везде стоит только как осознанное решение.

## Организация handlers

Предпочитай feature-oriented handler files:

```text
Handlers/
  Tickets/
    CreateTicketHandlers.cs
    TicketAdminHandlers.cs
    TicketCallbackHandlers.cs
  Profile/
    ProfileHandlers.cs
  Help/
    HelpHandlers.cs
```

Не делай один огромный `BotHandlers` class. Его сложно ревьюить, тестировать и проверять на registration conflicts.

## Namespaces

Держи namespaces рядом с ownership:

```csharp
namespace Company.Bot.Handlers.Tickets;
namespace Company.Application.Tickets;
namespace Company.Domain.Tickets;
namespace Company.Infrastructure.Persistence;
```

Не нужно механически повторять каждую папку, если names становятся шумными, но namespace не должен скрывать ответственность.

## Configuration

Configuration models держи рядом с application entry point:

```text
Configuration/
  TelegramSettings.cs
  StorageSettings.cs
```

Bind и validate делай на startup, а в TeleFlow передавай явные значения:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = telegram.BotToken;
    options.BotUsername = telegram.BotUsername;
});
```

Смотри [Конфигурация и секреты](../getting-started/configuration.md).

## Testing shape

Практичная структура test projects:

```text
Company.Bot.Tests/
  Handlers/
  Filters/
  Scenes/
  Registration/

Company.Application.Tests/
  Tickets/
  Users/
```

Application services по возможности тестируй без Telegram. Handlers тестируй там, где важны routing, context helpers, callback payloads, state transitions или Telegram-specific behavior.

## Что не делать слишком рано

Не начинай с этого в первый день:

- делить всё на microservices без deployment reason;
- прятать каждый `ctx.Bot.*Async` за собственный wrapper;
- создавать repository interfaces до второго implementation или test seam;
- делать каждую папку отдельным project;
- складывать handlers, services и models в один `Common` или `Core`.

Цель не в церемонии. Цель в коде, который может расти и не становиться мутным.

