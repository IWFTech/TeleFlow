# Generated registration

В TeleFlow есть два поддерживаемых пути регистрации handlers для нового кода:

1. generated assembly registration;
2. explicit direct registration.

Deprecated reflection assembly API остаётся только как временный migration path до `1.0`.

Рекомендуемый default для application code - generated assembly registration.

## Зачем нужен generated registration

Assembly scanning удобен, но runtime reflection как скрытый default усложняет startup reasoning. TeleFlow сохраняет удобство, но переносит metadata creation в build time.

Практический эффект:

- missing metadata ловится на startup;
- generated metadata можно проверять analyzers;
- reflection не является silent fallback для assembly registration.

## Настройка

Project file:

```xml
<PackageReference Include="IWF.TeleFlow.Telegram.Framework.LongPolling" Version="..." />
<PackageReference Include="IWF.TeleFlow.Generators" Version="..." PrivateAssets="all" />
```

Startup:

```csharp
builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();
```

Handler:

```csharp
public sealed class StartHandler
{
    [Command("start")]
    public Task Handle(MessageContext ctx, CancellationToken ct)
    {
        return ctx.Message.AnswerAsync("Ready.", ct);
    }
}
```

## Семантика ошибок

`AddTelegramHandlersFromAssembly(...)` ожидает generated Telegram handler metadata в target assembly.

Если metadata не найдена, он бросает exception. Он не сканирует assembly через reflection молча.

Это важно для production code. Missing generator package - configuration mistake, а не причина менять runtime behavior.

## Контракт регистрации

TeleFlow намеренно разделяет assembly registration и explicit type registration.

`AddTelegramHandlersFromAssembly(assembly)` означает:

- зарегистрировать generated metadata для этого assembly;
- требовать `IWF.TeleFlow.Generators`;
- упасть, если generated metadata отсутствует;
- никогда не сканировать assembly как fallback.

`AddTelegramHandler<THandler>()` означает:

- зарегистрировать ровно указанный handler type;
- построить metadata для этого type на startup;
- не сканировать containing assembly;
- не требовать generator.

`AddTelegramModule<TModule>()` означает:

- зарегистрировать ровно указанный module type;
- требовать `[TelegramModule]` на module type;
- использовать generated metadata для этого module, если она доступна;
- иначе построить metadata для этого module type на startup;
- не сканировать containing assembly.

Direct path сделан явным специально. Он удобен для tests, маленьких examples
и manually composed modules. Для больших приложений это не recommended default,
потому что каждый handler нужно перечислять руками.

## Прямая регистрация

Direct registration не требует generator:

```csharp
builder.Services.AddTelegramHandler<StartHandler>();
builder.Services.AddTelegramModule<AdminHandlers>();
```

Используй её для:

- маленьких examples;
- tests;
- manually composed modules;
- случаев, когда handler list должен быть очевиден на registration site.

## Deprecated reflection registration

Reflection assembly registration объявлен deprecated и будет удалён до `1.0`:

```csharp
builder.Services.AddTelegramHandlersFromAssemblyReflection(typeof(Program).Assembly);
```

Не используй его в новых проектах. Для обычных приложений используй generated assembly registration, а для ручного контроля - direct registration с явным списком handlers/modules.

## Что генерируется

Generated metadata описывает:

- handler type and method;
- route kind;
- route attributes;
- built-in filter metadata;
- state and scene metadata;
- callback payload metadata;
- error handlers;
- auto-answer callback metadata;
- generated invokers.

Обычно generated types напрямую не вызываются.

## Analyzer feedback

`IWF.TeleFlow.Generators` также содержит analyzer checks для invalid handler shapes, route usage, callback payloads, scene state definitions и error handler signatures.

Держи analyzer warnings видимыми в CI. Это часть framework contract.

Текущие diagnostic ids:

| Id | Что означает |
| --- | --- |
| `TLF001` | Несколько route attributes на одном handler method. |
| `TLF002` | Неподдерживаемый handler return type. |
| `TLF003` | Invalid или missing context parameter. |
| `TLF004` | Больше одного `CancellationToken`. |
| `TLF005` | Invalid command name. |
| `TLF006` | Text filter используется на callback handler. |
| `TLF007` | Duplicate command handler. |
| `TLF008` | Invalid handler type. |
| `TLF009` | Invalid handler method. |
| `TLF010` | Invalid callback data payload. |
| `TLF011` | Duplicate callback data prefix. |
| `TLF012` | Invalid state group. |
| `TLF013` | Invalid typed state reference. |
| `TLF014` | Invalid typed callback handler. |
| `TLF015` | Invalid Telegram module. |
| `TLF016` | Invalid route template. |
| `TLF017` | Invalid route regex. |
| `TLF018` | Invalid route value binding. |
| `TLF019` | Invalid filter usage. |
| `TLF020` | Handler method унаследован с другого type. |
| `TLF021` | Invalid command prefix. |
| `TLF022` | Invalid scene. |
| `TLF023` | Invalid scene step. |
| `TLF024` | Invalid auto-answer callback usage. |
| `TLF025` | Invalid class-based handler. |
| `TLF026` | Invalid error handler. |

## Рекомендуемая policy

Для production apps:

- используй generated assembly registration by default;
- держи `IWF.TeleFlow.Generators` private через `PrivateAssets="all"`;
- используй direct registration в tests, где это улучшает clarity;
- используй reflection assembly registration только как deprecated migration path во время миграции.
