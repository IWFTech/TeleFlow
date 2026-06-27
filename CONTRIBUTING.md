# Contributing to TeleFlow

TeleFlow is built as a framework, not a demo. Contributions should keep the runtime explicit, predictable, testable, and easy to debug.

## Prerequisites

- .NET 10 SDK. The repository uses `global.json` with `rollForward: latestFeature`.
- PowerShell 7+ for `eng/*.ps1` scripts.
- Git with line endings configured normally. The repository stores text files as LF through `.gitattributes`.

## Local Verification

Run from the repository root:

```powershell
dotnet restore ./TeleFlow.sln
dotnet format whitespace ./TeleFlow.sln --verify-no-changes --no-restore --verbosity minimal
dotnet format style ./TeleFlow.sln --verify-no-changes --no-restore --verbosity minimal
dotnet build ./TeleFlow.sln -c Release --no-restore /nodeReuse:false
./eng/verify-strict-analyzers.ps1 -Configuration Release -NoRestore
dotnet test ./TeleFlow.sln -c Release --no-build --no-restore /nodeReuse:false --logger "console;verbosity=minimal"
```

Run before release packaging changes:

```powershell
./eng/verify-release.ps1 -PackageVersion <version>
```

Run before changing `TeleFlow.Generators` Roslyn dependencies:

```powershell
./eng/verify-minimum-sdk-generator.ps1 -Configuration Release -RequiredSdkVersionPrefix "10.0.1"
```

`TeleFlow.Generators` is loaded by the user's compiler as an analyzer/source generator. Keep its `Microsoft.CodeAnalysis.*` references compatible with the minimum supported .NET SDK compiler band. A newer GitHub runner SDK is not enough proof of compatibility; package smoke tests must also pass on the minimum SDK feature band.

## Pull Request Expectations

- Branch from `main` and open pull requests back into `main` unless a maintainer explicitly asks for a release branch target.
- Keep changes focused.
- Add or update tests for behavior changes.
- Update docs when behavior, public APIs, package graph, runtime semantics, or workflow changes.
- Keep `TeleFlow.Core` transport-agnostic.
- Keep Telegram-specific behavior in Telegram packages.
- Prefer explicit behavior over hidden conventions.
- Do not introduce runtime reflection when source generation or explicit registration is the intended path.
- Do not hide failures behind silent fallback behavior.

## Generated Code

Generated Telegram schema and client output is checked into the repository. Do not hand-edit generated files as a normal fix path.

The Telegram schema generator is maintained separately in `IWFTech/TelegramSchemaGenerator`.

When generated output changes, the change should include enough context to review:
- Telegram Bot API source/version;
- generator version or generator change;
- generated diff;
- tests or package smoke coverage affected by the generated surface.

## Annotation Package Layout

`TeleFlow.Annotations` is physically grouped by responsibility: routing, callbacks, filters, state, scenes, chat member events, common primitives, and error handling. Keep the public namespace as `TeleFlow.Annotations` for every public annotation type. Folder names are for maintainers; they are not part of the public C# API shape.

Public annotation types must have XML documentation. The annotations project treats missing XML comments as build errors so published packages keep useful IntelliSense for application developers.

## Security

Do not open public issues with exploit details, tokens, or private bot data. Follow `SECURITY.md`.

## Commit Style

Use concise, imperative commit messages when possible:

```text
docs(ci): add release verification workflow
fix(dispatch): preserve cancellation during callback routing
test(state): cover wizard cancellation data cleanup
```
