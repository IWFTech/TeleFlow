# TeleFlow

[![CI](https://github.com/IWFTech/TeleFlow/actions/workflows/ci.yml/badge.svg)](https://github.com/IWFTech/TeleFlow/actions/workflows/ci.yml)
[![CodeQL](https://github.com/IWFTech/TeleFlow/actions/workflows/codeql.yml/badge.svg)](https://github.com/IWFTech/TeleFlow/actions/workflows/codeql.yml)
[![Telegram Bot API](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FIWFTech%2FTeleFlow%2Fmain%2Fdocs%2Fbadges%2Ftelegram-bot-api.json)](https://core.telegram.org/bots/api-changelog)
[![NuGet](https://img.shields.io/nuget/vpre/IWF.TeleFlow.Telegram.Framework.LongPolling?label=NuGet)](https://www.nuget.org/packages/IWF.TeleFlow.Telegram.Framework.LongPolling/)
[![Downloads](https://img.shields.io/nuget/dt/IWF.TeleFlow.Telegram.Framework.LongPolling?label=downloads)](https://www.nuget.org/packages/IWF.TeleFlow.Telegram.Framework.LongPolling/)
[![Telegram Chat](https://img.shields.io/badge/Telegram-chat-26A5E4?logo=telegram&logoColor=white)](https://t.me/teleflow_chat)
[![License](https://img.shields.io/github/license/IWFTech/TeleFlow)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

TeleFlow is an explicit Telegram bot framework for .NET.

It is built for the normal lifecycle of a Telegram bot: a first command, then callbacks, state, role checks, background services, retries, storage, diagnostics, deployment, and a codebase that still needs to be readable after it grows.

> Starts like a script. Grows like a system.

## Documentation

The repository documentation is split like a small product site and is ready to be moved to GitHub Pages later.

- [Documentation home](docs/README.md)
- [English documentation](docs/en/index.md)
- [Russian documentation](docs/ru/index.md)
- [Quickstart](docs/en/getting-started/quickstart.md)
- [Configuration and secrets](docs/en/getting-started/configuration.md)
- [Recommended paths](docs/en/getting-started/recommended-paths.md)
- [Package guide](docs/en/getting-started/packages.md)
- [Support desk tutorial](docs/en/tutorials/support-desk.md)
- [Enterprise guide](docs/en/enterprise/index.md)
- [Feature reference](docs/en/reference/attributes.md)
- [Roadmap](docs/en/roadmap.md)

## Community

- [Telegram chat](https://t.me/teleflow_chat) for quick questions, ideas, and alpha feedback.
- [GitHub Issues](https://github.com/IWFTech/TeleFlow/issues) for reproducible bugs and tracked feature requests.

## What TeleFlow Gives You

- Handler routing with attributes such as `[Command]`, `[Text]`, `[Callback]`, `[State]`, `[SceneStep]`, and media filters.
- Build-time generated handler metadata through the `IWF.TeleFlow.Generators` package.
- A deliberate failure when generated metadata is missing. No silent reflection fallback on the recommended path.
- Direct Telegram Bot API access through `ITelegramClient` and generated `ctx.Bot.*Async` extension methods.
- Message and callback helpers for common flows: answers, replies, edits, deletion, media, keyboards, and chat actions.
- Typed callback payloads with compact callback data serialization.
- State, state data, wizard navigation, and replaceable storage contracts.
- Long polling and ASP.NET Core webhook framework adapters.
- Raw long polling and raw webhook packages for applications that do not want the handler framework.
- Normal .NET dependency injection for handlers, services, repositories, filters, storage, and infrastructure.

## Install

TeleFlow is currently published as a public alpha. Use `--prerelease` or pin an exact alpha version.

For a handler-based long polling bot:

```bash
dotnet add package IWF.TeleFlow.Telegram.Framework.LongPolling --prerelease
dotnet add package IWF.TeleFlow.Generators --prerelease
dotnet add package IWF.TeleFlow.Storage.Memory --prerelease
```

Keep the generator as a private build-time dependency:

```xml
<PackageReference Include="IWF.TeleFlow.Generators" Version="..." PrivateAssets="all" />
```

For direct Bot API access without the framework:

```bash
dotnet add package IWF.TeleFlow.Telegram --prerelease
```

## First Bot

Create a console app, install the packages above, and set `TELEFLOW_BOT_TOKEN`:

```bash
export TELEFLOW_BOT_TOKEN=123456:token
```

PowerShell:

```powershell
$env:TELEFLOW_BOT_TOKEN = "123456:token"
```

Replace `Program.cs` with this:

```csharp
using TeleFlow.Annotations;
using TeleFlow.Core.Application;
using TeleFlow.Storage.Memory;
using TeleFlow.Telegram;

var token = Environment.GetEnvironmentVariable("TELEFLOW_BOT_TOKEN")
    ?? throw new InvalidOperationException("TELEFLOW_BOT_TOKEN is not set.");

var builder = TeleFlowApplication.CreateBuilder(args);

builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddMemoryStateStorage();
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();

await using var app = builder.Build();
await app.RunAsync();

public sealed class StartHandler
{
    [Command("start")]
    public Task Handle(MessageContext ctx, CancellationToken ct)
    {
        return ctx.Message.AnswerAsync("Hello from TeleFlow.", ct);
    }
}
```

This example uses generated assembly registration. If the `IWF.TeleFlow.Generators` package is not referenced by the application project, `AddTelegramHandlersFromAssembly(...)` fails during startup with a clear configuration error.

## Recommended Reading Order

If you are new to bot frameworks, read:

1. [Quickstart](docs/en/getting-started/quickstart.md)
2. [Configuration and secrets](docs/en/getting-started/configuration.md)
3. [Recommended paths](docs/en/getting-started/recommended-paths.md)
4. [Handlers and routing](docs/en/fundamentals/handlers-and-routing.md)
5. [Callbacks and keyboards](docs/en/features/callbacks-and-keyboards.md)
6. [State and wizard](docs/en/features/state-and-wizard.md)

If you already build production .NET services, read:

1. [Packages](docs/en/getting-started/packages.md)
2. [Configuration and secrets](docs/en/getting-started/configuration.md)
3. [Application model](docs/en/fundamentals/application-model.md)
4. [Project structure](docs/en/fundamentals/project-structure.md)
5. [Dependency injection](docs/en/fundamentals/dependency-injection.md)
6. [Transports](docs/en/transports/long-polling.md)
7. [Deployment](docs/en/enterprise/deployment.md)
8. [Performance and scaling](docs/en/enterprise/performance.md)
9. [Versioning and releases](docs/en/enterprise/versioning.md)
10. [Enterprise guide](docs/en/enterprise/index.md)

## Project Status

TeleFlow is in active development. The documentation describes APIs that exist in the current repository. Planned features belong in roadmap documents, not in user-facing API documentation.

Planned framework work is tracked in the [roadmap](docs/en/roadmap.md).

Runtime packages currently target `net10.0`. The `IWF.TeleFlow.Generators` package targets `netstandard2.0` because analyzers and source generators run inside the compiler.

## Benchmarks

The repository includes an isolated BenchmarkDotNet project for no-network runtime measurements:

- [Benchmark methodology and commands](benchmarks/README.md)

Benchmark dependencies are intentionally kept outside `TeleFlow.sln` and do not affect normal library builds or package graphs.

## License

TeleFlow is released under the MIT License.
