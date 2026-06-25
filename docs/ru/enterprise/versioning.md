# Версионирование и релизы

TeleFlow должен быть предсказуемым для upgrade. Versioning policy - часть framework contract.

## Package versioning

Пакеты TeleFlow обычно должны выпускаться как один согласованный version set:

```text
IWF.TeleFlow.Telegram
IWF.TeleFlow.Telegram.Client
IWF.TeleFlow.Telegram.Schema
IWF.TeleFlow.Telegram.Framework
IWF.TeleFlow.Telegram.Framework.LongPolling
IWF.TeleFlow.Telegram.Framework.Webhooks
IWF.TeleFlow.Telegram.LongPolling
IWF.TeleFlow.Telegram.Webhooks
IWF.TeleFlow.Core
IWF.TeleFlow.Annotations
IWF.TeleFlow.Generators
IWF.TeleFlow.Storage.Memory
```

Держи package versions aligned, если нет documented reason делать иначе. Согласованные versions уменьшают support friction и упрощают enterprise dependency review.

NuGet package IDs используют prefix `IWF.TeleFlow.*`. Public C# namespaces остаются `TeleFlow.*`.

## NuGet publishing

Release verification и NuGet publishing намеренно разделены:

- `Release Verify` делает restore, tests, pack, проверяет package metadata и загружает package artifacts.
- `NuGet Publish` запускает ту же release verification и может отправить packages на nuget.org, только если это явно включено.

Для alpha releases используй workflow `NuGet Publish`:

1. Укажи `packageVersion` в SemVer prerelease формате, например `0.1.0-alpha.1`.
2. Оставь `publishToNuGet` выключенным для dry verification run.
3. Включай `publishToNuGet` только когда packages реально нужно отправить на nuget.org.
4. Перед публикацией добавь repository secret `NUGET_API_KEY`.

Publish workflow использует `eng/verify-release.ps1`, поэтому package не будет опубликован, пока не пройдут restore, format verification, build, strict analyzer verification, tests, pack и package metadata checks.

## До 1.0

До `1.0.0` TeleFlow ещё может менять public APIs, пока foundation финализируется.

Даже до `1.0.0` changes должны быть intentional:

- breaking changes нужно документировать в release notes;
- migration examples лучше vague notes;
- core concepts нельзя ломать casually;
- generated и reflection behavior должны оставаться aligned там, где оба paths существуют.

## После 1.0

После `1.0.0` используем SemVer-style expectations:

| Version change | Что означает |
| --- | --- |
| Patch | Bug fixes, docs, diagnostics, compatible performance fixes. |
| Minor | New features и compatible public APIs. |
| Major | Breaking public API, runtime semantics, package graph или generated metadata behavior. |

## Что считается breaking

Breaking changes:

- удаление или rename public types, methods, properties, attributes или packages;
- изменение handler matching semantics;
- изменение state, wizard, callback, transport или error handling behavior так, что application result может измениться;
- изменение generated registration requirements;
- изменение package dependencies так, что existing consumers ломаются;
- изменение target frameworks;
- изменение analyzer diagnostics из advisory в blocking так, что ранее valid code перестаёт собираться.

Не каждый internal refactor является breaking. Internal implementation может меняться, если public behavior остаётся прежним.

## Deprecation policy

Когда возможно:

1. Добавь replacement API.
2. Пометь старый API obsolete с конкретным message.
3. Сохрани его минимум на один minor release.
4. Удали его в следующем major release.

Не держи temporary APIs вечно. У deprecated API должен быть понятный removal plan.

## Telegram Bot API schema updates

Telegram может выпускать Bot API changes независимо от TeleFlow releases.

Schema updates могут затрагивать:

- generated Telegram DTOs;
- generated method models;
- generated `ITelegramClient` extension methods;
- update type metadata;
- serialization behavior.

Policy:

- schema updates нужно автоматизировать насколько возможно;
- generated output должен проходить review перед release;
- schema changes, которые только добавляют новые Telegram fields или methods, могут быть minor releases;
- schema changes, которые rename или reshape existing public generated types, могут требовать major release, даже если upstream behavior поменял Telegram.

## Generator updates

`IWF.TeleFlow.Generators` - build-time dependency, но generator changes могут влиять на runtime registration.

Generator releases должны держать эти contracts:

- `AddTelegramHandlersFromAssembly(...)` требует generated metadata;
- на recommended path нет silent fallback на reflection;
- analyzer diagnostics должны объяснять invalid user code;
- generated и reflection paths должны оставаться behaviorally aligned, когда оба поддерживают один scenario.

## Release notes

Каждый release должен включать:

- changed packages;
- new features;
- bug fixes;
- breaking changes;
- migration notes;
- Telegram Bot API schema version или source snapshot, когда relevant;
- known limitations.

## Consumer upgrade guidance

Application teams должны:

- pin package versions;
- обновлять все TeleFlow packages вместе;
- запускать tests после updates;
- проверять generated registration в startup tests;
- читать release notes перед minor и major upgrades;
- не смешивать prerelease packages со stable packages, если это не intentional testing.
