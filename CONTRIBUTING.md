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

The Telegram schema generator is maintained separately in `IWFTech/TeleFlow.Telegram.SchemaGenerator`.

When generated output changes, the change should include enough context to review:
- Telegram Bot API source/version;
- generator version or generator change;
- generated diff;
- tests or package smoke coverage affected by the generated surface.

## Security

Do not open public issues with exploit details, tokens, or private bot data. Follow `SECURITY.md`.

## Commit Style

Use concise, imperative commit messages when possible:

```text
docs(ci): add release verification workflow
fix(dispatch): preserve cancellation during callback routing
test(state): cover wizard cancellation data cleanup
```
