# Package Guide

TeleFlow packages are split by responsibility. Application code usually imports the same namespace:

```csharp
using TeleFlow.Telegram;
```

NuGet package IDs use the `IWF.TeleFlow.*` prefix. C# namespaces stay concise for user code.

## Quick Choice

Most applications install a small set of entry packages. The rest of the package graph exists to keep internal boundaries clean and is usually restored transitively.

### Recommended Entry Packages

| Scenario | Package reference | What you get |
| --- | --- | --- |
| Handler framework with long polling | `IWF.TeleFlow.Framework.LongPolling` | Handler framework plus long-polling transport |
| Handler framework with webhooks | `IWF.TeleFlow.Framework.Webhooks` | Handler framework plus ASP.NET Core webhook transport |
| Direct Telegram Bot API access | `IWF.TeleFlow.Telegram` | Low-level `ITelegramClient`, generated Bot API methods, defaults, JSON, transport, exceptions, deep links |
| Generated handler metadata | `IWF.TeleFlow.Generators` | Source generator and analyzer package for build-time handler registration. Reference it directly with `PrivateAssets="all"` |
| In-memory state storage | `IWF.TeleFlow.Storage.Memory` | Process-local state, state data, wizard history, and state middleware registration |
| Generic Host integration | `IWF.TeleFlow.Framework.Hosting` | Optional `IHostedService` adapter for running a configured TeleFlow application through Microsoft.Extensions.Hosting |
| Project Fluent localization | `IWF.TeleFlow.Framework.I18n.Fluent` | Optional startup-loaded Fluent catalogs, scoped Telegram locale resolution, safe HTML/MarkdownV2 formatting, and explicit-locale background formatting |

### Advanced And Dependency Packages

| Scenario | Package reference | When to reference directly |
| --- | --- | --- |
| Framework runtime without bundled transport | `IWF.TeleFlow.Framework` | Only when you provide a custom framework update source or transport package |
| Framework primitives | `IWF.TeleFlow.Framework.Core` | Normally never in bot projects. Storage and framework packages reference it transitively |
| Direct client runtime boundary | `IWF.TeleFlow.Telegram.Client` | Only when you intentionally want the explicit lower-level client package instead of the owner package `IWF.TeleFlow.Telegram` |
| Generated Telegram schema | `IWF.TeleFlow.Telegram.Schema` | Only when you intentionally work with generated Telegram DTOs and method models without the client or framework runtime |
| Handler attributes | `IWF.TeleFlow.Annotations` | Normally restored through framework packages. Reference directly only for advanced compile-only scenarios |
| Raw long polling without handlers | `IWF.TeleFlow.Telegram.LongPolling` | When you want `getUpdates` and acknowledged update streaming over raw Telegram `Update` values |
| Raw ASP.NET Core webhooks without handlers | `IWF.TeleFlow.Telegram.Webhooks` | When you want ASP.NET Core endpoint helpers over raw Telegram `Update` values |
| Localization engine boundary | `IWF.TeleFlow.Framework.I18n` | When implementing a localization adapter other than Fluent. Normal Fluent applications receive it transitively |

## Installing Alpha Packages

TeleFlow is currently published as a public alpha. Use `--prerelease` with `dotnet add package`, or pin an exact alpha version in the project file.

Recommended alpha install for a small long polling bot:

```bash
dotnet add package IWF.TeleFlow.Framework.LongPolling --prerelease
dotnet add package IWF.TeleFlow.Generators --prerelease
dotnet add package IWF.TeleFlow.Storage.Memory --prerelease
```

Add Generic Host integration only when the application is a worker service or an ASP.NET Core host:

```bash
dotnet add package IWF.TeleFlow.Framework.Hosting --prerelease
```

Add Fluent localization only when the bot needs translated user-facing text:

```bash
dotnet add package IWF.TeleFlow.Framework.I18n.Fluent --prerelease
```

See [Localization with Fluent](../features/localization.md) for resource layout, locale resolution, safe formatting, and background-service usage.

## Recommended Defaults

For a handler-based long polling bot:

```xml
<PackageReference Include="IWF.TeleFlow.Framework.LongPolling" Version="..." />
<PackageReference Include="IWF.TeleFlow.Generators" Version="..." PrivateAssets="all" />
<PackageReference Include="IWF.TeleFlow.Storage.Memory" Version="..." />
```

For a worker-service style application, add hosting:

```xml
<PackageReference Include="IWF.TeleFlow.Framework.Hosting" Version="..." />
```

```csharp
using Microsoft.Extensions.Hosting;
using TeleFlow.Framework.Hosting;
using TeleFlow.Storage.Memory;
using TeleFlow.Telegram;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddMemoryStateStorage();
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();
builder.Services.AddTeleFlowHostedService();

await builder.Build().RunAsync();
```

For a smaller console-style long polling bot without Generic Host:

```xml
<PackageReference Include="IWF.TeleFlow.Framework.LongPolling" Version="..." />
<PackageReference Include="IWF.TeleFlow.Generators" Version="..." PrivateAssets="all" />
<PackageReference Include="IWF.TeleFlow.Storage.Memory" Version="..." />
```

```csharp
using TeleFlow.Framework.Application;
using TeleFlow.Storage.Memory;
using TeleFlow.Telegram;

var builder = TeleFlowApplication.CreateBuilder(args);

builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddMemoryStateStorage();
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();

await using var app = builder.Build();
await app.RunAsync();
```

For a handler-based webhook bot:

```xml
<PackageReference Include="IWF.TeleFlow.Framework.Webhooks" Version="..." />
<PackageReference Include="IWF.TeleFlow.Generators" Version="..." PrivateAssets="all" />
```

```csharp
using TeleFlow.Telegram;
using TeleFlow.Telegram.Webhooks;

builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddWebhook(options => options.Path = "/telegram");

app.MapTelegramWebhook();
```

For direct Bot API access only:

```xml
<PackageReference Include="IWF.TeleFlow.Telegram" Version="..." />
```

```csharp
using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Telegram;

services.AddTelegramClient(options => options.Token = token);

var bot = provider.GetRequiredService<ITelegramClient>();
var me = await bot.GetMeAsync();
```

## Generated Registration Dependency

Use the `IWF.TeleFlow.Generators` package when the application calls:

```csharp
services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
```

This API expects generated metadata in the target assembly. If metadata is missing, startup fails with a clear exception. It does not silently scan the assembly with reflection.

Explicit direct registration does not require the generator:

```csharp
services.AddTelegramHandler<StartHandler>();
services.AddTelegramModule<AdminModule>();
```

Direct registration registers only the named type. It builds metadata for that
type at startup and does not scan the containing assembly. `AddTelegramModule<T>()`
uses generated metadata when available, then falls back to direct metadata for
the named module type only.

Reflection assembly registration is deprecated and will be removed before `1.0`:

```csharp
services.AddTelegramHandlersFromAssemblyReflection(typeof(Program).Assembly);
```

Do not use it for new projects. Use generated assembly registration, or register handlers/modules explicitly when the handler list should be manual.

## Client-Only Applications

Use the `IWF.TeleFlow.Telegram` package when the application only needs to call Telegram Bot API methods and does not need handlers, dispatcher, filters, state, long polling, or webhooks.

```xml
<PackageReference Include="IWF.TeleFlow.Telegram" Version="..." />
```

Use `IWF.TeleFlow.Telegram.Client` only when you want the explicit owner package name:

```xml
<PackageReference Include="IWF.TeleFlow.Telegram.Client" Version="..." />
```

Both packages expose the same normal namespace:

```csharp
using TeleFlow.Telegram;
```

## Raw Transport Applications

Use raw transport packages when you want Telegram `Update` values directly and do not want the handler framework.

Raw long polling:

```xml
<PackageReference Include="IWF.TeleFlow.Telegram.LongPolling" Version="..." />
```

Raw webhooks:

```xml
<PackageReference Include="IWF.TeleFlow.Telegram.Webhooks" Version="..." />
```

Raw long polling uses Telegram `allowed_updates` string values. Framework long polling uses `TelegramAllowedUpdates` and handler metadata to select update types.

## State Storage

The `IWF.TeleFlow.Storage.Memory` package is useful for local development, examples, and process-local bots:

```csharp
using TeleFlow.Storage.Memory;

services.AddMemoryStateStorage();
```

It registers:

- `IStateStore`
- `IStateDataStore`
- `IStateDataSerializer`
- `IStateHistoryStore`
- state middleware

For production deployments with multiple processes, restarts, or external workers, replace storage contracts with your own implementation.

## Target Framework

Runtime packages currently target `net10.0`. The `IWF.TeleFlow.Generators` package targets `netstandard2.0` because analyzer and generator packages run inside the C# compiler.
