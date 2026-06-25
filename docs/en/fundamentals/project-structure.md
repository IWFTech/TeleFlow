# Project Structure

TeleFlow does not force one application architecture. A bot can start as one console app and grow into a service with clear boundaries.

Use the smallest structure that keeps ownership obvious.

## Small Bot

For a small bot, keep one project:

```text
EchoBot/
  Program.cs
  Handlers/
    StartHandler.cs
    EchoHandler.cs
  appsettings.json
```

This is enough when the bot has a few commands, no complex domain model, and process-local state is acceptable.

Rules:

- one handler class per use case;
- keep DI registrations in `Program.cs`;
- do not introduce repositories until there is storage;
- do not split projects before the code needs a boundary.

## Product Bot

For a real product, keep the bot layer explicit and move business logic out of handlers:

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

Recommended ownership:

| Folder | Owns |
| --- | --- |
| `Handlers` | Telegram routing and user interaction. |
| `Scenes` | Conversation flows and wizard steps. |
| `Filters` | Telegram-specific handler filters. |
| `Callbacks` | Typed callback payload DTOs. |
| `Application` | Use cases and application services. |
| `Domain` | Product concepts that should not depend on Telegram. |
| `Infrastructure` | Storage, external clients, adapters, and technical implementations. |

Handlers should orchestrate. They should not become the place where all business rules live.

## Large Bot

For a large bot, split projects when it creates a real boundary:

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

Keep Telegram DTOs mostly in `Company.Bot` and `Company.Infrastructure.Telegram`. If the whole product is Telegram-only, passing Telegram ids through application services is fine. Passing full Telegram schema objects everywhere should be a conscious decision.

## Handler Organization

Prefer feature-oriented handler files:

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

Avoid one giant `BotHandlers` class. It becomes hard to review, hard to test, and hard to reason about registration conflicts.

## Namespaces

Keep namespaces aligned with ownership:

```csharp
namespace Company.Bot.Handlers.Tickets;
namespace Company.Application.Tickets;
namespace Company.Domain.Tickets;
namespace Company.Infrastructure.Persistence;
```

Do not mirror every folder mechanically when it makes names noisy, but avoid namespaces that hide responsibility.

## Configuration

Keep configuration models close to the application entry point:

```text
Configuration/
  TelegramSettings.cs
  StorageSettings.cs
```

Bind and validate them during startup, then pass explicit values into TeleFlow:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = telegram.BotToken;
    options.BotUsername = telegram.BotUsername;
});
```

See [Configuration and secrets](../getting-started/configuration.md).

## Testing Shape

A practical test project layout:

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

Test application services without Telegram whenever possible. Test handlers where routing, context helpers, callback payloads, state transitions, or Telegram-specific behavior matter.

## What Not To Do Early

Avoid these on day one:

- splitting into microservices before there is a deployment reason;
- abstracting every `ctx.Bot.*Async` call behind your own wrapper;
- creating repository interfaces before a second implementation or a test seam exists;
- making every folder a separate project;
- putting all handlers, services, and models into one `Common` or `Core` folder.

The goal is not ceremony. The goal is code that can grow without becoming unclear.

