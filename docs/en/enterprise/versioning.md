# Versioning And Releases

TeleFlow should be predictable to upgrade. Versioning policy is part of the framework contract.

## Package Versioning

TeleFlow packages should normally be released as one coherent version set:

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
IWF.TeleFlow.Hosting
IWF.TeleFlow.Annotations
IWF.TeleFlow.Generators
IWF.TeleFlow.Storage.Memory
```

Keep package versions aligned unless there is a documented reason not to. Aligned versions reduce support friction and make enterprise dependency reviews simpler.

NuGet package IDs use the `IWF.TeleFlow.*` prefix. Public C# namespaces stay `TeleFlow.*`.

## NuGet Publishing

Release verification and NuGet publishing are intentionally separate:

- `Release Verify` builds, tests, packs, validates package metadata, and uploads package artifacts.
- `NuGet Publish` runs the same release verification and can push packages to nuget.org when explicitly requested.

Use the `NuGet Publish` workflow for alpha releases:

1. Set `packageVersion` to a SemVer prerelease version such as `0.1.0-alpha.1`.
2. Keep `publishToNuGet` disabled for a dry verification run.
3. Enable `publishToNuGet` only when the packages should be pushed to nuget.org.
4. Configure a NuGet Trusted Publishing policy for owner `iriswolf`, repository `IWFTech/TeleFlow`, workflow file `nuget-publish.yml`, and environment `nuget-production`.

The publish workflow uses `NuGet/login@v1` with GitHub OIDC. It does not require a long-lived NuGet API key in GitHub Secrets.
The `nuget-production` GitHub environment is the release boundary for real package publishing and should allow deployments from `main` only.

The publish workflow uses `eng/verify-release.ps1`, so a package is not pushed unless restore, format verification, build, strict analyzer verification, tests, pack, and package metadata checks complete first.

## Before 1.0

Before `1.0.0`, TeleFlow can still change public APIs while the foundation is being finalized.

Even before `1.0.0`, changes should be intentional:

- document breaking changes in release notes;
- prefer migration examples over vague notes;
- avoid breaking core concepts casually;
- keep generated and reflection behavior aligned where both paths exist.

## After 1.0

After `1.0.0`, use SemVer-style expectations:

| Version change | Meaning |
| --- | --- |
| Patch | Bug fixes, docs, diagnostics, compatible performance fixes. |
| Minor | New features and compatible public APIs. |
| Major | Breaking public API, runtime semantics, package graph, or generated metadata behavior. |

## What Counts As Breaking

Breaking changes include:

- removing or renaming public types, methods, properties, attributes, or packages;
- changing handler matching semantics;
- changing state, wizard, callback, transport, or error handling behavior in a way that can change application results;
- changing generated registration requirements;
- changing package dependencies in a way that breaks existing consumers;
- changing target frameworks;
- changing analyzer diagnostics from advisory to blocking in a way that breaks previously valid code.

Not every internal refactor is breaking. Internal implementation can change when public behavior stays the same.

## Deprecation Policy

When possible:

1. Add the replacement API.
2. Mark the old API obsolete with a concrete message.
3. Keep it for at least one minor release.
4. Remove it in the next major release.

Do not keep temporary APIs forever. A deprecated API should have a clear removal plan.

## Telegram Bot API Schema Updates

Telegram can release Bot API changes independently of TeleFlow releases.

Schema updates can affect:

- generated Telegram DTOs;
- generated method models;
- generated `ITelegramClient` extension methods;
- update type metadata;
- serialization behavior.

Policy:

- schema updates should be automated as much as possible;
- generated output must be reviewed before release;
- schema changes that only add new Telegram fields or methods can be minor releases;
- schema changes that rename or reshape existing public generated types can require a major release, even if Telegram changed upstream behavior.

## Generator Updates

`IWF.TeleFlow.Generators` is a build-time dependency, but generator changes can affect runtime registration.

Generator releases must keep these contracts clear:

- `AddTelegramHandlersFromAssembly(...)` requires generated metadata;
- no silent fallback to reflection on the recommended path;
- analyzer diagnostics should explain the invalid user code;
- generated and reflection paths should stay behaviorally aligned when both support the same scenario.

## Release Notes

Each release should include:

- changed packages;
- new features;
- bug fixes;
- breaking changes;
- migration notes;
- Telegram Bot API schema version or source snapshot when relevant;
- known limitations.

## Consumer Upgrade Guidance

Application teams should:

- pin package versions;
- update all TeleFlow packages together;
- run tests after updates;
- verify generated registration during startup tests;
- read release notes before minor and major upgrades;
- avoid mixing prerelease packages with stable packages unless intentionally testing.
