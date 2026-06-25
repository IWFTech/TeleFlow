# Changelog

TeleFlow follows SemVer for published NuGet packages and documented public behavior.

## Unreleased

### Added

- GitHub Actions CI workflow for format, build, strict analyzer, and test verification.
- GitHub Actions release verification workflow for package validation artifacts.
- GitHub Actions NuGet publish workflow for explicit package publication to nuget.org through Trusted Publishing.
- Cross-platform repository health baseline: SDK pinning, contribution guide, security policy, changelog, issue templates, pull request template, CODEOWNERS, Dependabot configuration, and CodeQL workflow.

### Changed

- CI is intended to run on Windows, Linux, and macOS for normal pull request verification.
- NuGet package IDs use the `IWF.TeleFlow.*` prefix while public C# namespaces stay `TeleFlow.*`.
