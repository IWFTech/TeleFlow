# Generated Registration

TeleFlow has two supported handler registration paths for new code:

1. generated assembly registration;
2. explicit direct registration.

A deprecated reflection assembly API still exists as a temporary migration path before `1.0`.

The recommended default for application code is generated assembly registration.

## Why Generated Registration Exists

Assembly scanning is convenient, but runtime reflection as a hidden default makes startup harder to reason about. TeleFlow keeps the convenience while moving metadata creation to build time.

That gives three practical benefits:

- missing metadata is caught during startup;
- generated metadata can be checked by analyzers;
- reflection is not a silent fallback for assembly registration.

## Setup

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

## Failure Semantics

`AddTelegramHandlersFromAssembly(...)` expects generated Telegram handler metadata in the target assembly.

If metadata is not found, it throws. It does not silently scan the assembly with reflection.

This is important for production code. A missing generator package is a configuration mistake, not a reason to change runtime behavior.

## Registration Contract

TeleFlow intentionally keeps assembly registration and explicit type registration separate.

`AddTelegramHandlersFromAssembly(assembly)` means:

- register generated metadata for that assembly;
- require `IWF.TeleFlow.Generators`;
- fail if generated metadata is missing;
- never scan the assembly for handlers as a fallback.

`AddTelegramHandler<THandler>()` means:

- register exactly the named handler type;
- build metadata for that type at startup;
- do not scan the containing assembly;
- do not require the generator.

`AddTelegramModule<TModule>()` means:

- register exactly the named module type;
- require `[TelegramModule]` on the module type;
- use generated metadata for that module when available;
- otherwise build metadata for that module type at startup;
- do not scan the containing assembly.

The direct path is explicit by design. It is useful for tests, tiny examples,
and manually composed modules. It is not the recommended default for large
applications because every handler must be listed manually.

## Direct Registration

Direct registration does not need the generator:

```csharp
builder.Services.AddTelegramHandler<StartHandler>();
builder.Services.AddTelegramModule<AdminHandlers>();
```

Use it for:

- small examples;
- tests;
- manually composed modules;
- situations where the handler list should be obvious at the registration site.

## Deprecated Reflection Registration

Reflection assembly registration is deprecated and will be removed before `1.0`:

```csharp
builder.Services.AddTelegramHandlersFromAssemblyReflection(typeof(Program).Assembly);
```

Do not use it for new projects. Use generated assembly registration for normal applications, or direct registration when the handler list should be explicit at the registration site.

## What Gets Generated

The generated metadata describes:

- handler type and method;
- route kind;
- route attributes;
- built-in filter metadata;
- state and scene metadata;
- callback payload metadata;
- error handlers;
- auto-answer callback metadata;
- generated invokers.

You normally do not call generated types directly.

## Analyzer Feedback

`IWF.TeleFlow.Generators` also includes analyzer checks for invalid handler shapes, route usage, callback payloads, scene state definitions, and error handler signatures.

Keep analyzer warnings visible in CI. They are part of the framework contract.

Current diagnostic ids:

| Id | Meaning |
| --- | --- |
| `TLF001` | Multiple route attributes on one handler method. |
| `TLF002` | Unsupported handler return type. |
| `TLF003` | Invalid or missing context parameter. |
| `TLF004` | More than one `CancellationToken`. |
| `TLF005` | Invalid command name. |
| `TLF006` | Text filter used on callback handler. |
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
| `TLF020` | Handler method inherited from another type. |
| `TLF021` | Invalid command prefix. |
| `TLF022` | Invalid scene. |
| `TLF023` | Invalid scene step. |
| `TLF024` | Invalid auto-answer callback usage. |
| `TLF025` | Invalid class-based handler. |
| `TLF026` | Invalid error handler. |

## Recommended Policy

For production apps:

- use generated assembly registration by default;
- keep `IWF.TeleFlow.Generators` private with `PrivateAssets="all"`;
- use direct registration in tests where it improves clarity;
- treat reflection assembly registration as a deprecated migration path only.
