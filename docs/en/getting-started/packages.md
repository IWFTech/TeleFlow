# Package Guide

TeleFlow packages are split by responsibility. Application code usually imports the same namespace:

```csharp
using TeleFlow.Telegram;
```

NuGet package IDs use the `IWF.TeleFlow.*` prefix. C# namespaces stay concise for user code.

## Quick Choice

| Scenario | Package reference | What you get |
| --- | --- | --- |
| Direct Telegram Bot API access | `IWF.TeleFlow.Telegram` | Low-level `ITelegramClient`, generated Bot API methods, defaults, JSON, transport, exceptions, deep links |
| Direct client through the owner package | `IWF.TeleFlow.Telegram.Client` | Same client runtime through the explicit package-boundary name |
| Handler framework with long polling | `IWF.TeleFlow.Telegram.Framework.LongPolling` | Handler framework plus long-polling transport |
| Handler framework with webhooks | `IWF.TeleFlow.Telegram.Framework.Webhooks` | Handler framework plus ASP.NET Core webhook transport |
| Raw long polling without handlers | `IWF.TeleFlow.Telegram.LongPolling` | `getUpdates` runner and acknowledged update stream over raw Telegram `Update` values |
| Raw ASP.NET Core webhooks without handlers | `IWF.TeleFlow.Telegram.Webhooks` | ASP.NET Core endpoint helpers over raw Telegram `Update` values |
| In-memory state storage | `IWF.TeleFlow.Storage.Memory` | Process-local state, state data, wizard history, and state middleware registration |
| Handler attributes | `IWF.TeleFlow.Annotations` | Attributes such as `[Command]`, `[Text]`, `[Callback]`, `[State]`, and filters |
| Generated handler metadata | `IWF.TeleFlow.Generators` | Source generator and analyzer package for build-time handler registration |

`IWF.TeleFlow.Telegram.Schema` is normally pulled in by Telegram packages. Reference it directly only when you intentionally work with generated Telegram DTOs and method models without the client or framework runtime.

## Installing Alpha Packages

TeleFlow is currently published as a public alpha. Use `--prerelease` with `dotnet add package`, or pin an exact alpha version in the project file.

Recommended alpha install for a long polling bot:

```bash
dotnet add package IWF.TeleFlow.Telegram.Framework.LongPolling --prerelease
dotnet add package IWF.TeleFlow.Generators --prerelease
dotnet add package IWF.TeleFlow.Storage.Memory --prerelease
```

## Recommended Defaults

For a handler-based long polling bot:

```xml
<PackageReference Include="IWF.TeleFlow.Telegram.Framework.LongPolling" Version="..." />
<PackageReference Include="IWF.TeleFlow.Generators" Version="..." PrivateAssets="all" />
<PackageReference Include="IWF.TeleFlow.Storage.Memory" Version="..." />
```

```csharp
using TeleFlow.Core.Application;
using TeleFlow.Storage.Memory;
using TeleFlow.Telegram;

var builder = TeleFlowApplication.CreateBuilder(args);

builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddMemoryStateStorage();
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();
```

For a handler-based webhook bot:

```xml
<PackageReference Include="IWF.TeleFlow.Telegram.Framework.Webhooks" Version="..." />
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
