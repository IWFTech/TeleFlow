# Гайд по пакетам

Пакеты TeleFlow разделены по ответственности. В application code обычно используется один namespace:

```csharp
using TeleFlow.Telegram;
```

NuGet package IDs используют prefix `IWF.TeleFlow.*`. C# namespaces остаются короткими для пользовательского кода.

## Быстрый выбор

| Сценарий | Package reference | Что даёт |
| --- | --- | --- |
| Прямой доступ к Telegram Bot API | `IWF.TeleFlow.Telegram` | Low-level `ITelegramClient`, generated Bot API methods, defaults, JSON, transport, exceptions, deep links |
| Прямой client через owner package | `IWF.TeleFlow.Telegram.Client` | Тот же client runtime через явное package-boundary имя |
| Handler framework с long polling | `IWF.TeleFlow.Telegram.Framework.LongPolling` | Handler framework плюс long-polling transport |
| Handler framework с webhooks | `IWF.TeleFlow.Telegram.Framework.Webhooks` | Handler framework плюс ASP.NET Core webhook transport |
| Raw long polling без handlers | `IWF.TeleFlow.Telegram.LongPolling` | `getUpdates` runner и acknowledged update stream поверх raw Telegram `Update` values |
| Raw ASP.NET Core webhooks без handlers | `IWF.TeleFlow.Telegram.Webhooks` | ASP.NET Core endpoint helpers поверх raw Telegram `Update` values |
| In-memory state storage | `IWF.TeleFlow.Storage.Memory` | Process-local state, state data, wizard history и регистрация state middleware |
| Handler attributes | `IWF.TeleFlow.Annotations` | Атрибуты вроде `[Command]`, `[Text]`, `[Callback]`, `[State]` и filters |
| Generated handler metadata | `IWF.TeleFlow.Generators` | Source generator и analyzer package для build-time handler registration |

`IWF.TeleFlow.Telegram.Schema` обычно подтягивается Telegram-пакетами. Подключай его напрямую только если намеренно работаешь с generated Telegram DTOs и method models без client/framework runtime.

## Установка alpha packages

TeleFlow сейчас опубликован как public alpha. Используй `--prerelease` с `dotnet add package` или фиксируй конкретную alpha version в project file.

Рекомендуемая alpha-установка для long polling bot:

```bash
dotnet add package IWF.TeleFlow.Telegram.Framework.LongPolling --prerelease
dotnet add package IWF.TeleFlow.Generators --prerelease
dotnet add package IWF.TeleFlow.Storage.Memory --prerelease
```

## Рекомендуемый дефолт

Для handler-based long polling bot:

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

Для handler-based webhook bot:

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

Для прямого доступа к Bot API:

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

## Зависимость для generated registration

Используй package `IWF.TeleFlow.Generators`, когда приложение вызывает:

```csharp
services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
```

Этот API ожидает generated metadata в целевой assembly. Если metadata нет, startup падает с понятной ошибкой. Silent fallback на reflection не происходит.

Explicit direct registration не требует generator:

```csharp
services.AddTelegramHandler<StartHandler>();
services.AddTelegramModule<AdminModule>();
```

Direct registration регистрирует только указанный type. Он строит metadata для
этого type на startup и не сканирует containing assembly. `AddTelegramModule<T>()`
использует generated metadata, когда она доступна, а затем fallback на direct
metadata только для указанного module type.

Reflection assembly registration объявлен deprecated и будет удалён до `1.0`:

```csharp
services.AddTelegramHandlersFromAssemblyReflection(typeof(Program).Assembly);
```

Не используй его в новых проектах. Используй generated assembly registration, либо регистрируй handlers/modules явно, если список должен быть ручным.

## Client-only приложения

Используй package `IWF.TeleFlow.Telegram`, когда приложению нужно только вызывать Telegram Bot API methods без handlers, dispatcher, filters, state, long polling или webhooks.

```xml
<PackageReference Include="IWF.TeleFlow.Telegram" Version="..." />
```

`IWF.TeleFlow.Telegram.Client` нужен только если хочется явно указать owner package name:

```xml
<PackageReference Include="IWF.TeleFlow.Telegram.Client" Version="..." />
```

Оба пакета используют обычный namespace:

```csharp
using TeleFlow.Telegram;
```

## Raw transport приложения

Raw transport packages нужны, когда ты хочешь получать Telegram `Update` values напрямую и не хочешь handler framework.

Raw long polling:

```xml
<PackageReference Include="IWF.TeleFlow.Telegram.LongPolling" Version="..." />
```

Raw webhooks:

```xml
<PackageReference Include="IWF.TeleFlow.Telegram.Webhooks" Version="..." />
```

Raw long polling использует Telegram `allowed_updates` string values. Framework long polling использует `TelegramAllowedUpdates` и handler metadata для выбора update types.

## State storage

Package `IWF.TeleFlow.Storage.Memory` подходит для local development, examples и process-local bots:

```csharp
using TeleFlow.Storage.Memory;

services.AddMemoryStateStorage();
```

Он регистрирует:

- `IStateStore`
- `IStateDataStore`
- `IStateDataSerializer`
- `IStateHistoryStore`
- state middleware

Для production deployment с несколькими процессами, рестартами или внешними workers заменяй storage contracts своей реализацией.

## Target framework

Runtime-пакеты сейчас target `net10.0`. Package `IWF.TeleFlow.Generators` target `netstandard2.0`, потому что analyzer/generator packages запускаются внутри C# compiler.
