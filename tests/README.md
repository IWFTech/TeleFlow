# TeleFlow Test Architecture

This directory contains TeleFlow's automated test suite.

The suite is intentionally treated as framework architecture, not as a
collection of incidental regression tests. Tests should document the contracts
that TeleFlow promises to users and maintainers: public API shape, generated
metadata, runtime dispatch, Telegram request execution, state management,
transport behavior, diagnostics, and package boundaries.

## Current Layout

The repository currently uses one test project:

- `TeleFlow.ArchitectureTests`

Keeping one project is acceptable for now. The immediate goal is not to create
more projects. The goal is to make ownership clear so new tests land in the
right semantic area and future coverage reporting can be interpreted correctly.

Several existing files are large because they grew with the framework:

- `TelegramHandlerDispatcherTests.cs`
- `TelegramRuntimeIntegrationTests.cs`
- `TelegramHandlerGeneratorTests.cs`
- `StageSixStateAndMiddlewareTests.cs`
- `StageEightGeneratedRegistrationTests.cs`

Do not use those files as a model for new work. Add new tests according to the
taxonomy below. Large historical files should be split incrementally under
issue #44, not moved wholesale in one noisy change.

## Target Taxonomy

Use these semantic areas when adding or moving tests.

## Target Directory Layout

The long-term layout should follow semantic folders and small contract-focused
files. This is closer to the way large pytest suites stay readable: the
directory names describe the product area, and file names describe the specific
contract being tested.

Target shape:

```text
tests/TeleFlow.ArchitectureTests/
  PublicSurface/
    PackageGraphTests.cs
    PublicApiTests.cs
  TelegramClient/
    RequestExecution/
    RetryAndErrors/
    Multipart/
    Defaults/
  FrameworkRuntime/
    Dispatching/
    Routing/
    Filters/
    Middleware/
    ErrorHandling/
    Diagnostics/
    Context/
  CallbacksAndKeyboards/
    CallbackRouting/
    CallbackSerialization/
    KeyboardBuilder/
  StateAndWizard/
    State/
    StateData/
    Wizard/
    StateKeys/
  StateStorageContracts/
  GeneratedRegistration/
    Runtime/
    Metadata/
    ErrorHandlers/
    Filters/
  AnalyzerAndGenerator/
    Generator/
    Analyzer/
  Transports/
    LongPolling/
    Webhooks/
  Hosting/
  TestSupport/
```

This layout is the target for #44. The #69 scope is to document the ownership
model and make new tests follow it. Existing large files should be split
incrementally instead of being moved in one broad mechanical PR.

### PublicSurface

Owns public compatibility and package-shape contracts:

- package graph and project references;
- public namespaces;
- annotations contracts;
- public API compatibility checks;
- package smoke tests.

### TelegramClient

Owns low-level Telegram Bot API client behavior:

- request executor;
- transport abstraction;
- Telegram response envelope parsing;
- typed exceptions and error mapping;
- retry-after policy;
- multipart uploads;
- generated client methods;
- JSON options and Telegram wire names;
- client defaults and explicit argument override rules.

### FrameworkRuntime

Owns framework execution behavior:

- application bootstrap;
- update processor;
- dispatcher execution;
- route selection and route priority;
- handler invocation;
- built-in filters;
- custom filter execution;
- middleware pipeline;
- error handlers;
- current update and context accessors;
- dependency validation.

### CallbacksAndKeyboards

Owns callback and keyboard DX/runtime behavior:

- callback data metadata;
- generated callback codecs;
- callback route matching;
- raw string callback behavior;
- inline keyboard builder behavior;
- custom callback serialization boundaries;
- stale or invalid callback payload behavior.

### StateAndWizard

Owns user state and wizard behavior:

- state lifecycle;
- state data lifecycle;
- wizard navigation;
- state key isolation;
- storage policy replacement;
- state middleware interaction with Telegram contexts.

### StateStorageContracts

Owns reusable provider contracts:

- state store contracts;
- state data store contracts;
- state history store contracts;
- memory provider coverage;
- future Redis/Postgres provider reuse.

Provider-specific tests may cover provider-specific behavior, but every storage
provider must satisfy the shared contract tests.

### GeneratedRegistration

Owns generated framework metadata and registration behavior:

- generated handler registration;
- generated invokers;
- generated route metadata;
- generated filter metadata;
- generated state and scene metadata;
- generated error handlers;
- generated-only registration boundaries;
- generated versus manual registration behavior.

### AnalyzerAndGenerator

Owns compile-time behavior:

- source generator output shape;
- analyzer diagnostics;
- invalid handler diagnostics;
- invalid callback/filter/state/scene/module diagnostics;
- inheritance and abstract base handling;
- generated source compileability.

Runtime dispatch tests should not be hidden here unless the test explicitly
checks generated runtime integration.

### Transports

Owns update transport behavior:

- raw long polling;
- framework long polling;
- raw webhooks;
- framework webhooks;
- offset and acknowledgement semantics;
- cancellation behavior;
- transient and terminal failure semantics.

### Hosting

Owns host integration:

- hosted service integration;
- startup and shutdown tasks;
- DI lifetime validation around hosted runtime;
- host cancellation behavior.

### TestSupport

Owns test-only infrastructure:

- fake Telegram transports;
- recording loggers;
- generated registration probes;
- update builders;
- DI helpers;
- Roslyn compiler and generator helpers.

Test support code must stay boring. Helpers should remove noise from tests, not
hide the behavior being asserted.

## Placement Rules

- A test class should own one framework contract, not one historical milestone.
- New tests must be placed in the semantic area that owns the behavior.
- Regression tests should stay near the contract they protect.
- Generator and analyzer tests should assert compile-time behavior.
- Runtime tests should assert runtime behavior.
- Generated/reflection parity must be explicit in the test name or class name.
- Storage providers must reuse shared contract tests.
- Avoid broad "integration" files unless the scenario truly spans multiple
  contracts and cannot be tested clearly at a narrower boundary.

## Generated And Reflection Parity

Generated registration is the recommended path. Reflection registration exists
only where the framework still supports it explicitly.

When both paths are supposed to support a scenario, tests must show parity
directly. Do not assume that a generated-path test also proves the reflection
path, or the other way around.

Prefer names that make the tested path obvious, for example:

- `GeneratedRegistration_DispatchesCommandHandler`
- `ReflectionRegistration_DispatchesCommandHandler`
- `GeneratedAndReflectionRegistration_ApplySameRoutePriority`

If a feature is intentionally generated-only, document that in the test name or
the nearby test comment.

## Coverage Rules

CI collects coverage from the fast in-process suite on Linux. It validates
aggregate line and branch floors, checks changed-line coverage on pull requests,
and stores the generated HTML report as a GitHub Actions artifact. Use the same
scope locally:

```powershell
dotnet test ./tests/TeleFlow.ArchitectureTests/TeleFlow.ArchitectureTests.csproj -c Release --no-build --no-restore /nodeReuse:false --filter "Category!=PackageSmoke" --settings ./eng/coverage.runsettings --collect "XPlat Code Coverage" --results-directory ./artifacts/coverage
```

Coverage must answer whether important contracts are tested. A single global
percentage is not enough.

Coverage is most valuable for:

- dispatcher routing and priority;
- filters and middleware;
- error handling;
- state and wizard lifecycle;
- storage contracts;
- callback serialization and matching;
- Telegram request execution;
- retry-after and error mapping;
- long polling and webhook failure semantics;
- generated registration and analyzer diagnostics.

Coverage must not be padded by testing generated DTOs or trivial accessors.
Generated code, generated schema models, and build output should be excluded
from coverage unless a test intentionally verifies generated framework behavior.

`PackageSmoke` tests are required package-contract checks, but they execute
`dotnet pack`, restore, and consumer builds in child processes. They do not
belong in line coverage because Coverlet cannot attribute those child processes
to the test host meaningfully. CI runs them once on Linux outside the
cross-platform fast-test matrix.

The `Coverage` job is the pull request source of truth. It enforces aggregate
floors of 85% lines and 75% branches, plus 80% changed-line coverage. The
existing Docs Pages workflow independently rebuilds the same trusted coverage
scope from `main` and publishes the public HTML report and README SVG badge.
Pull requests never deploy or overwrite the public badge.

Do not add a second coverage SaaS, upload token, gist, bot commit, or mutable
coverage branch. GitHub Actions artifacts and the existing GitHub Pages site own
the reporting lifecycle.

## Current-To-Target Split Map

Use this map when splitting existing large files under issue #44.

- `TelegramHandlerDispatcherTests.cs`
  - `FrameworkRuntime/Dispatching`
  - `FrameworkRuntime/Routing`
  - `FrameworkRuntime/Filters`
  - `FrameworkRuntime/ErrorHandling`
  - `FrameworkRuntime/Diagnostics`
  - `CallbacksAndKeyboards/CallbackRouting`

- `TelegramRuntimeIntegrationTests.cs`
  - `TelegramClient/Registration`
  - `TelegramClient/RequestExecution`
  - `TelegramClient/RetryAndErrors`
  - `TelegramClient/Multipart`
  - `TelegramClient/Defaults`
  - `Transports/LongPolling`
  - `FrameworkRuntime/Context`

- `TelegramHandlerGeneratorTests.cs`
  - `AnalyzerAndGenerator/Generator`
  - `AnalyzerAndGenerator/Analyzer`
  - `GeneratedRegistration/Metadata`
  - `CallbacksAndKeyboards/GeneratedCallbackCodecs`

- `StageSixStateAndMiddlewareTests.cs`
  - `StateAndWizard/State`
  - `StateAndWizard/Wizard`
  - `StateAndWizard/StateKeys`
  - `FrameworkRuntime/Middleware`
  - `StateStorageContracts` when the test is provider-contract shaped

- `StageEightGeneratedRegistrationTests.cs`
  - `GeneratedRegistration/Runtime`
  - `GeneratedRegistration/ErrorHandlers`
  - `GeneratedRegistration/Filters`
  - `FrameworkRuntime/GeneratedDispatchParity`
  - `CallbacksAndKeyboards/GeneratedCallbackRouting`

- `StateStorageContracts/*`
  - keep as contract tests and use as the model for future storage providers.

## Refactoring Rules

When splitting a large test file:

1. Move one semantic area at a time.
2. Keep test names stable unless the old name is misleading.
3. Move only the helpers required by the moved tests.
4. Do not rewrite assertions during a mechanical move.
5. Run the affected test project after the move.
6. Keep production code unchanged unless the test exposes a real defect.

The goal is to reduce ownership ambiguity, not to create a large cosmetic diff.
