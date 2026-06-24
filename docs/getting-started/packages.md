# TeleFlow Packages

For direct Telegram Bot API access, install `TeleFlow.Telegram` and use the normal Telegram namespace.

In application code, the normal import is:

```csharp
using TeleFlow.Telegram;
```

Add transport or framework packages only when the application needs polling, webhooks, or handlers.

## Quick Choice

| Scenario | Package reference | What you get |
| --- | --- | --- |
| Only call Telegram Bot API, default package | `TeleFlow.Telegram` | Low-level `ITelegramClient`, generated Bot API methods, defaults, JSON, transport, exceptions, deep links |
| Only call Telegram Bot API, exact owner package | `TeleFlow.Telegram.Client` | Same client runtime through the explicit package-boundary name |
| Handler framework with long polling | `TeleFlow.Telegram.Framework.LongPolling` | Handler framework plus long-polling transport |
| Handler framework with webhooks | `TeleFlow.Telegram.Framework.Webhooks` | Handler framework plus ASP.NET Core webhook transport |
| Raw long polling without handlers | `TeleFlow.Telegram.LongPolling` | `getUpdates` runner and acknowledged update stream over raw Telegram `Update` values |
| Raw ASP.NET Core webhooks without handlers | `TeleFlow.Telegram.Webhooks` | ASP.NET Core endpoint helpers over raw Telegram `Update` values |

`TeleFlow.Telegram.Schema` is normally pulled in by the Telegram packages. Reference it directly only when you intentionally work with generated Telegram DTOs and method models without the client or runtime packages.

## Recommended Defaults

For applications that only call Telegram Bot API methods, start with `TeleFlow.Telegram`.

```xml
<PackageReference Include="TeleFlow.Telegram" Version="..." />
```

```csharp
using TeleFlow.Telegram;

services.AddTelegramClient(options =>
{
    options.Token = token;
});

var bot = provider.GetRequiredService<ITelegramClient>();
var me = await bot.GetMeAsync();
```

This package is intentionally client-first. It does not install the handler framework, long polling adapter, or webhook adapter.

For handler-based applications, reference the exact framework transport package:

```xml
<PackageReference Include="TeleFlow.Telegram.Framework.LongPolling" Version="..." />
<PackageReference Include="TeleFlow.Generators" Version="..." PrivateAssets="all" />
```

or:

```xml
<PackageReference Include="TeleFlow.Telegram.Framework.Webhooks" Version="..." />
<PackageReference Include="TeleFlow.Generators" Version="..." PrivateAssets="all" />
```

These packages still use the same namespace:

```csharp
using TeleFlow.Telegram;
```

`TeleFlow.Generators` is build-time tooling for generated handler registration. Use it when the application calls `AddTelegramHandlersFromAssembly(...)`.

Direct registration with `AddTelegramHandler<THandler>()` does not require `TeleFlow.Generators`. Assembly registration with `AddTelegramHandlersFromAssembly(...)` requires generated metadata and fails fast when the generator package is missing.

`TeleFlow.Generators` runs inside the application's C# compiler. The package is built against the minimum Roslyn compiler version TeleFlow supports, not automatically against the newest available Roslyn package. This keeps generated handler registration usable for users on the supported .NET SDK feature band.

## Client-Only Applications

Use `TeleFlow.Telegram` when the application only needs to call Telegram Bot API methods and does not need handlers, dispatcher, filters, state, long polling, or webhooks.

```xml
<PackageReference Include="TeleFlow.Telegram" Version="..." />
```

Use `TeleFlow.Telegram.Client` only when you want the explicit package-boundary name instead of the default convenience package:

```xml
<PackageReference Include="TeleFlow.Telegram.Client" Version="..." />
```

```csharp
using TeleFlow.Telegram;

services.AddTelegramClient(options =>
{
    options.Token = token;
});

var bot = provider.GetRequiredService<ITelegramClient>();
var me = await bot.GetMeAsync();
```

These packages are for applications, workers, or services that send Bot API requests directly.

## Raw Transport Applications

Use raw transport packages when you want Telegram `Update` values directly and do not want the framework handler model.

Raw long polling:

```xml
<PackageReference Include="TeleFlow.Telegram.LongPolling" Version="..." />
```

Raw webhooks:

```xml
<PackageReference Include="TeleFlow.Telegram.Webhooks" Version="..." />
```

Raw long polling uses Telegram `allowed_updates` string values:

```csharp
var options = new TelegramRawLongPollingOptions
{
    AllowedUpdates = ["message", "callback_query"]
};
```

Use framework long polling when you want `TelegramAllowedUpdates` and handler-based update selection.

## Framework Applications

Use framework adapter packages when you want TeleFlow handlers and a specific transport.

Long polling:

```xml
<PackageReference Include="TeleFlow.Telegram.Framework.LongPolling" Version="..." />
<PackageReference Include="TeleFlow.Generators" Version="..." PrivateAssets="all" />
```

```csharp
using TeleFlow.Telegram;

services.AddTelegramBot(options => options.Token = token);
services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
services.AddLongPolling();
```

Webhooks:

```xml
<PackageReference Include="TeleFlow.Telegram.Framework.Webhooks" Version="..." />
<PackageReference Include="TeleFlow.Generators" Version="..." PrivateAssets="all" />
```

```csharp
using TeleFlow.Telegram;
using TeleFlow.Telegram.Webhooks;

services.AddTelegramBot(options => options.Token = token);
services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
services.AddWebhook(options => options.Path = "/telegram");

app.MapTelegramWebhook();
```

## Why Package Names Differ From Namespace

TeleFlow keeps application imports simple:

- application code gets one predictable `using TeleFlow.Telegram;`;
- package references choose the installed runtime surface;
- `TeleFlow.Telegram` stays client-first and avoids framework runtime dependencies;
- raw transport apps avoid handlers and dispatcher;
- framework apps compose the client plus the selected transport adapter.

This keeps everyday code quiet while still allowing explicit dependencies when an application needs them.
