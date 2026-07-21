# Changelog

TeleFlow follows SemVer for published NuGet packages and documented public behavior.

## Unreleased

## 1.0.0-alpha.11 - 2026-07-21

### Added

- Added `TelegramBotApiEnvironment` to configure production or Telegram Bot API test-environment requests through both `AddTelegramClient(...)` and `AddTelegramBot(...)`.
- Added test-environment endpoint construction for all outgoing Bot API methods, including custom root `BaseUrl` configurations.

### Changed

- Documented the separate Telegram test-account and test-bot credential requirement, the test endpoint path, and the current payment-update boundary.

## 1.0.0-alpha.10 - 2026-07-21

### Added

- Added CI guardrails for generated Telegram schema/client updates, requiring changelog acknowledgement and validating Telegram Bot API manifest, badge, and generated header consistency.

### Changed

- Updated the generated Telegram schema and client surface to Telegram Bot API 10.2.

### Fixed

- Fixed the `CallbackQueryActions.AnswerAsync` overload that accepted callback text and could recurse instead of sending the callback answer.
- Fixed prefix-less short command routes so ordinary text beginning with the same word does not trigger the command.

## 1.0.0-alpha.9 - 2026-07-02

This alpha focuses on Telegram client response execution internals: clearer request execution stages and less JSON rematerialization on successful Bot API responses.

### Changed

- Split `TelegramRequestExecutor` request processing into explicit send, parse, retry-after, success deserialization, API failure, and decode failure stages.
- Telegram response envelopes are now parsed from UTF-8 response bytes instead of first materializing the whole HTTP body as a `string`.
- Successful Telegram response `result` payloads are now deserialized from the parsed `JsonElement` instead of using `GetRawText()` and deserializing a second JSON string.
- `HttpClientTelegramTransport` now reads Telegram response bodies as bytes and passes owned response buffers into the client pipeline.
- Custom transport documentation now describes the byte-based response body contract in English and Russian.

### Fixed

- Request diagnostics, retry-after handling, decode failure mapping, API failure mapping, cancellation, and `getUpdates` diagnostic suppression now have more focused regression coverage.
- Documentation home page example spacing was adjusted after the alpha.8 docs site update.

### Breaking Changes

- `TelegramTransportResponse.Body` changed from `string` to `ReadOnlyMemory<byte>`.
  Existing custom transports can still construct responses from `string`, but code that reads `Body` directly must decode the UTF-8 bytes explicitly.

## 1.0.0-alpha.8 - 2026-07-02

This alpha focuses on production diagnostics, bounded Telegram retry-after handling, scoped current-update access, state storage key isolation, and explicit command prefix routing.

### Added

- Added `TelegramRetryAfterPolicy` for bounded automatic handling of Telegram `429 retry_after` responses.
- Added `IUpdateContextAccessor` for scoped access to the current transport-agnostic update during framework processing.
- Added `ITelegramCurrentUpdateAccessor` for scoped access to the current Telegram update, user, chat, message, callback query, chat member update, and state key.
- Added runtime dependency validation for handler, error handler, and custom filter service parameters.
- Added state storage key isolation contracts through `IStateStorageKeyBuilder`, `DefaultStateStorageKeyBuilder`, `StateStorageKeyPart`, and `StateKeyDefaults`.
- Added explicit state key customization helpers for production storage providers and multi-bot applications.
- Added `CommandPrefixMode` for `[Command]`, `[CommandTemplate]`, and `[CommandRegex]` routes, allowing command prefixes to be required, optional, or disabled for prefix-less command-shaped text.
- Added safe diagnostics for filter rejections, webhook invalid secret/payload handling, webhook update failures, and Telegram request failure exception types.

### Changed

- Telegram request execution now retries only bounded short `retry_after` responses by default and throws `TelegramRetryAfterException` when the retry policy refuses to wait.
- Long polling startup delivery guidance now recommends an explicit startup task calling `DeleteWebhookAsync(dropPendingUpdates: true)` instead of hiding destructive delivery behavior in long polling options.
- State storage keys now have a documented, escaped, partition-aware format that storage providers can share consistently.
- Generated handler metadata now carries command prefix mode when a route opts into non-default command prefix behavior.
- Framework update rate limiting now uses explicit accept/reject decisions instead of exception-based throttling.
- Handler, error-handler, and filter dependencies now fail during runtime validation with targeted configuration errors instead of failing later during update handling where possible.
- Documentation now covers retry-after defaults, scoped current-update accessors, middleware/service lifetimes, state key customization, rate-limit decision behavior, and logging privacy rules.

### Breaking Changes

- `IUpdateRateLimiter.WaitAsync(...)` was replaced with `IUpdateRateLimiter.CheckAsync(...)`, returning `UpdateRateLimitDecision`.
- Custom update rate limiters must return `UpdateRateLimitDecision.Accepted` or `UpdateRateLimitDecision.Rejected(...)` instead of relying on exceptions for normal rejection.

## 1.0.0-alpha.7 - 2026-07-01

This alpha focuses on typed callback data hardening: compile-time callback data codecs, clearer stale callback diagnostics, and validation before the `1.0.0` stabilization line.

### Added

- Source-generated callback data codecs for `[CallbackData]` payloads when `IWF.TeleFlow.Generators` is installed.
- Generated callback packing, matching, and unpacking for typed inline keyboard payloads and typed callback routes.
- Warning diagnostics for compact typed callback data that matches a typed route shape but cannot be decoded.
- EN/RU documentation for stale callback buttons, raw callback fallbacks, and callback payload versioning guidance.

### Changed

- Default typed callback serialization now prefers generated codecs and falls back to runtime metadata only when generated metadata is unavailable.
- Malformed compact typed callback data is treated as an unmatched typed route after a warning, allowing raw callback fallback handlers to answer old or stale buttons.
- Generated enum callback fields now decode through explicit validation instead of surfacing low-level enum parse failures.

### Fixed

- Oversized `[CallbackData]` prefixes are reported by analyzer/runtime validation instead of failing later during keyboard packing.
- Stale compact typed callback payloads no longer look like random handler failures.

## 1.0.0-alpha.6 - 2026-07-01

This alpha focuses on Microsoft.Extensions.Hosting integration, application lifecycle tasks, package graph naming stabilization, and release packaging hardening.

### Added

- Optional `IWF.TeleFlow.Framework.Hosting` package for running a configured TeleFlow application through `Microsoft.Extensions.Hosting` as an `IHostedService`.
- Application startup and shutdown task contracts with DI registration helpers.
- Lifecycle execution around update source startup and shutdown, including scoped task resolution, deterministic ordering, and failure propagation.

### Changed

- Framework runtime package names were stabilized before `1.0.0`:
  - `IWF.TeleFlow.Core` became `IWF.TeleFlow.Framework.Core`.
  - `IWF.TeleFlow.Telegram.Framework` became `IWF.TeleFlow.Framework`.
  - `IWF.TeleFlow.Telegram.Framework.LongPolling` became `IWF.TeleFlow.Framework.LongPolling`.
  - `IWF.TeleFlow.Telegram.Framework.Webhooks` became `IWF.TeleFlow.Framework.Webhooks`.
- Framework runtime namespaces now use `TeleFlow.Framework.*` instead of `TeleFlow.Core.*`.
- Documentation and package descriptions now separate recommended entry packages from dependency and advanced packages.
- Middleware options configuration is documented as normal .NET options configuration.
- Release verification now checks that its packaged project list stays aligned with package smoke tests.

### Fixed

- Release verification now includes `IWF.TeleFlow.Framework.Hosting`, preventing a publish run from accidentally omitting the hosting package.

### Deprecated

- The old alpha package IDs were deprecated on NuGet with replacement package guidance:
  - `IWF.TeleFlow.Core` -> `IWF.TeleFlow.Framework.Core`.
  - `IWF.TeleFlow.Telegram.Framework` -> `IWF.TeleFlow.Framework`.
  - `IWF.TeleFlow.Telegram.Framework.LongPolling` -> `IWF.TeleFlow.Framework.LongPolling`.
  - `IWF.TeleFlow.Telegram.Framework.Webhooks` -> `IWF.TeleFlow.Framework.Webhooks`.

### Breaking Changes

- Applications using old alpha framework package IDs must migrate to the new `IWF.TeleFlow.Framework.*` package IDs.
- Applications using `TeleFlow.Core.*` namespaces must migrate to `TeleFlow.Framework.*` namespaces.

## 1.0.0-alpha.5 - 2026-06-30

This alpha focuses on handler registration semantics, compile-time filter metadata, route dispatch indexing, and typed callback keyboard packing.

### Added

- Parameterized custom filter attributes with generated metadata, constructor arguments, and named arguments.
- Generator diagnostics for handler methods that define state, role, chat member, or filter metadata without any route attribute.

### Changed

- Handler route candidates are pre-indexed once and reused during dispatch to reduce repeated per-update scans.
- Generated assembly registration, explicit direct registration, and deprecated reflection assembly registration are now documented as separate contracts.
- Explicit handler and module registration semantics are documented for manual registration without generated metadata.
- Typed inline keyboard callback payloads now pack through `[CallbackData]` metadata with `InlineKeyboardBuilder.Build()`, while raw callback strings are preserved exactly.
- Callback keyboard serialization ownership is explicit: custom `ICallbackDataSerializer` remains available for advanced callback serialization and route deserialization, while typed keyboard buttons use callback metadata directly.

### Fixed

- Filter-only handler methods are reported at build time instead of being silently ignored by generated registration.

### Deprecated

- Reflection-based assembly handler registration is deprecated in favor of generated registration or explicit direct registration.

### Breaking Changes

- `InlineKeyboardBuilder.Build(ICallbackDataSerializer)` was removed. Use `InlineKeyboardBuilder.Build()` and pass the resulting native `InlineKeyboardMarkup` to Telegram methods or message helpers.

## 1.0.0-alpha.4 - 2026-06-30

This alpha focuses on runtime hot-path reductions, scoped middleware lifetimes, explicit keyboard markup ownership, generated Bot API constants, and release workflow hardening.

### Added

- Telegram announcement workflow for release, pull request, and issue notifications.
- Generated Bot API discriminator constants adoption for documented keyboard button styles.

### Changed

- Update middleware is resolved from the per-update scope, so middleware can depend on scoped application services such as repositories.
- Inline keyboard creation now uses an explicit `InlineKeyboardBuilder.Build()` step that returns native `InlineKeyboardMarkup`.
- Handler timing details are collected only when debug logging is enabled.
- State hydration snapshot semantics were clarified after review, including how failed storage reads interact with cached update state.
- State data and wizard wrappers are created lazily per update.
- Telegram error handlers are pre-indexed for exception-path dispatch.
- Handler selection, filter evaluation, typed callback route misses, compact callback metadata lookup, and JSON-only request content building now avoid several avoidable runtime allocations or scans.
- Generated Telegram schema output was refreshed with grouped discriminator constants.
- Telegram announcement summaries were reformatted for clearer public issue, pull request, and release messages.
- GitHub Actions dependencies and NuGet symbol package publishing were hardened.

### Fixed

- Scoped repositories and services can now be consumed from update middleware without singleton lifetime validation failures.
- Callback route candidate checks no longer use exception-driven no-match flow for typed callback payloads.
- JSON-only Telegram requests no longer recursively scan payload values when the request type cannot contain upload streams.
- Duplicate NuGet symbol package push behavior was removed from the publish workflow.

### Breaking Changes

- Inline keyboard builders must be converted to native markup explicitly with `.Build()` before being passed to Telegram methods or message helpers.

### Notes

- Final typed keyboard callback serialization ownership is intentionally not finalized in this alpha. The design work continues in `1.0.0-alpha.5`.

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
