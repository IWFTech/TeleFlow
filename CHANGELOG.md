# Changelog

TeleFlow follows SemVer for published NuGet packages and documented public behavior.

## Unreleased

No unreleased changes.

## 1.0.0-alpha.3 - 2026-06-28

This alpha focuses on framework trust, state consistency, package readability, and documentation polish.

### Added

- XML documentation for public `TeleFlow.Annotations` APIs.
- Package verification that ensures `IWF.TeleFlow.Annotations` includes XML documentation in the NuGet package.
- Reusable state storage contract tests for current and future storage providers.
- GitHub Pages documentation site and benchmark documentation.

### Changed

- `ctx.State` now caches the current state snapshot for the duration of one update, including the no-state `null` case.
- `TeleFlow.Annotations` source files are grouped by responsibility while the public `TeleFlow.Annotations` namespace remains stable.
- Documentation navigation, theme contrast, README badges, package onboarding, and quickstart examples were refined.

### Fixed

- Repeated current-state reads in one update no longer cause repeated storage reads.
- State snapshot failure semantics are explicit: failed storage calls do not update the cached snapshot.
- Quickstart and command template examples were corrected for current handler registration usage.

### Breaking Changes

- None.

## 1.0.0-alpha.2 - 2026-06-26

### Changed

- Published the alpha package line under the `1.0.0-alpha.*` version scheme.
- Improved NuGet package onboarding documentation and package metadata.
- Kept NuGet package IDs under the `IWF.TeleFlow.*` prefix while public C# namespaces stay `TeleFlow.*`.

### Breaking Changes

- None.

## 0.1.0-alpha.1 - 2026-06-26

Initial public alpha.

### Added

- Public NuGet packages for the TeleFlow framework, Telegram client, generated schema, annotations, source generators, transports, and memory storage.
- GitHub Actions CI workflow for format, build, strict analyzer, and test verification.
- GitHub Actions release verification workflow for package validation artifacts.
- GitHub Actions NuGet publish workflow for explicit package publication to nuget.org through Trusted Publishing.
- Cross-platform repository health baseline: SDK pinning, contribution guide, security policy, changelog, issue templates, pull request template, CODEOWNERS, Dependabot configuration, and CodeQL workflow.

### Changed

- CI is intended to run on Windows, Linux, and macOS for normal pull request verification.
- CI and CodeQL skip heavy verification steps when a change touches only documentation or non-code repository files.

### Breaking Changes

- None.
