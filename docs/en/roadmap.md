# TeleFlow Roadmap

This document tracks planned framework work. It is intentionally separate from the feature documentation: pages outside the roadmap describe APIs that exist in the repository today.

## Current Rules

- Roadmap items are not public API commitments until they are implemented, tested, and documented in the relevant feature pages.
- Planned features must preserve TeleFlow's explicit runtime model.
- Convenience APIs must not hide behavior that affects retries, rate limits, state consistency, delivery guarantees, or handler execution order.
- Enterprise-oriented features must have replaceable storage and observable failure behavior.

## Rate Limiting And Retry Policies

### Handler-Level Rate Limiting

Status: planned.

Current state:

- TeleFlow supports custom handler filters through `[UseFilter<TFilter>]`.
- `[UseFilter<TFilter>]` can be applied to a handler method or to a handler class.
- Custom filters can implement cooldowns today, but this requires user code and does not provide a standard rejection response.
- `IUpdateRateLimiter` exists for incoming update middleware, but it runs before handler selection and is not the right final API for per-handler policies.

Target state:

- Add a first-class handler rate limiting API for individual handlers and handler groups.
- Allow policies such as per-user, per-chat, per-user-per-chat, per-command, and custom keys.
- Support memory storage for simple bots and a replaceable distributed storage contract for production deployments.
- Provide explicit rejection behavior: skip silently, answer the user, throw a typed exception, or call a configured rejection delegate.
- Preserve generated/reflection registration parity.
- Keep policy application visible in handler metadata so debugging does not depend on hidden runtime conventions.

Possible public shape:

```csharp
[Command("start")]
[RateLimit("start-command")]
public Task Start(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Hello.", ct);
}
```

```csharp
[RateLimit("support-commands")]
public sealed class SupportHandlers
{
    [Command("ticket")]
    public Task Ticket(MessageContext ctx, CancellationToken ct) => Task.CompletedTask;

    [Command("profile")]
    public Task Profile(MessageContext ctx, CancellationToken ct) => Task.CompletedTask;
}
```

```csharp
builder.Services.AddTelegramRateLimiting(options =>
{
    options.AddPolicy("start-command", policy => policy
        .PerChat()
        .PerUser()
        .PerCommand()
        .FixedWindow(TimeSpan.FromSeconds(15))
        .OnRejected(async context =>
        {
            await context.Message.AnswerAsync("Please wait before using this command again.");
        }));
});
```

Expected outcome:

- Method-level and class-level policies both work.
- Multiple policies on the same handler have deterministic order.
- Generated handler metadata includes rate limiting descriptors.
- Reflection registration and generated registration produce the same route behavior.
- Rejection behavior is explicit and test-covered.
- Cancellation is respected while waiting on storage or rejection callbacks.
- Memory storage is available for local/small bots.
- Distributed storage can be implemented without replacing the handler dispatcher.
- Documentation contains examples for junior, production, and enterprise usage.

Non-goals for the first implementation:

- No hidden global throttling of all Telegram API calls.
- No automatic retry of non-idempotent Telegram API requests beyond Telegram `Retry-After` handling.
- No implicit distributed behavior without explicit storage registration.

### Outgoing Telegram API Rate Limiting

Status: planned after handler-level rate limiting.

Current state:

- Telegram `429` responses with `response_parameters.retry_after` or HTTP `Retry-After` are respected by the Telegram request executor.
- Ordinary non-429 Telegram API failures are not retried automatically.

Target state:

- Add explicit outgoing API rate limiting policies keyed by bot, method, chat, user, or a custom key.
- Keep retries and throttling observable through logs and metrics.
- Avoid hidden duplicate sends for non-idempotent operations.

## Stable Release Readiness

Status: planned before the first stable release.

Current state:

- TeleFlow has public alpha packages, generated Telegram client methods, handler routing, state/wizard APIs, callbacks, long polling, webhooks, error handlers, documentation, release workflows, and no-network benchmarks.
- Package boundaries are explicit: client, schema, framework, raw transports, and framework transport adapters are separate packages.
- Public APIs may still change before the first stable release.

Target state:

- Review and freeze public API names, package graph, handler metadata, diagnostics, and registration behavior.
- Remove or mark transitional APIs before stable packages are published.
- Keep `TeleFlow.Core` transport-agnostic, generated schema DTOs framework-free, and framework runtime behavior inside Telegram framework packages.
- Keep generated assembly registration as the recommended production path, with explicit reflection registration remaining opt-in.
- Make quickstart, package guide, enterprise docs, samples, and release notes match the actual packages and APIs.

Expected outcome:

- CI, release verification, package graph tests, analyzer tests, and representative sample builds are green.
- Generated registration and explicit reflection registration have parity for supported framework semantics.
- Documentation describes current behavior only; planned features remain in this roadmap.
- Release notes state alpha/stable status, breaking-change rules, supported .NET versions, and Telegram Bot API schema version.

## Startup And Delivery Controls

Status: planned.

Current state:

- Long polling preserves at-least-once update processing by advancing offset only after update processing succeeds.
- Public docs do not claim a `drop_pending_updates` option.
- Deep-link URL building uses explicit bot username configuration and does not call `getMe` lazily.

Target state:

- Add explicit startup delivery controls for long polling, including a documented `drop_pending_updates` equivalent with exact offset and acknowledgement semantics.
- Evaluate an opt-in startup bot identity resolver that can call `getMe` once, cache the bot identity, and fail or warn according to explicit configuration.
- Keep lazy network calls out of helpers such as deep-link builders.

Expected outcome:

- Pending-update behavior is test-covered and documented for long polling startup.
- Startup identity resolution has deterministic failure behavior.
- Bot identity lookup does not happen implicitly during handler execution or deep-link URL construction.

## State Flow Ergonomics

Status: planned after the current state and wizard contracts are stabilized.

Current state:

- TeleFlow supports explicit current state through `ctx.State`.
- TeleFlow supports state data through `ctx.State.Data` and `ctx.Wizard.Data`.
- State data is currently accessed by string keys such as `"quiz"`.
- `Wizard.GoToAsync(state)` records navigation history and is best suited for linear flows and explicit back navigation.
- Dynamic loops can be implemented today by staying in the same state, updating state data, and rendering the next prompt manually.
- There is no first-class state enter hook and no explicit re-enter/self-transition API.

Target state:

- Add typed state data helpers so common flow data can be accessed without repeating string keys.
- Evaluate an explicit `StateData<T>` handler parameter that makes state-backed data visible and distinct from DI services, route values, and callback payloads.
- Add state enter hooks for prompts that should run when a state is entered.
- Add an explicit re-enter/self-transition API that invokes state enter behavior without polluting wizard history.
- Document dynamic state-loop patterns for quizzes, paginated menus, retry-until-valid-input flows, and long-running support workflows.

Possible public shape:

```csharp
[State("quiz:question")]
[HasText]
public async Task Answer(
    MessageContext ctx,
    StateData<QuizRunData> quiz,
    IQuizEngine engine,
    CancellationToken ct)
{
    var run = await quiz.GetRequiredAsync(ct);

    engine.ApplyAnswer(run, ctx.Message.Text!);

    if (engine.IsCompleted(run))
    {
        await quiz.ClearAsync(ct);
        await ctx.State.ClearAsync(ct);
        await ctx.Message.AnswerAsync("Quiz completed.", ct);
        return;
    }

    await quiz.SetAsync(run, ct);
    await ctx.State.ReEnterAsync(ct);
}
```

```csharp
[StateEnter("quiz:question")]
public async Task EnterQuestion(
    MessageContext ctx,
    StateData<QuizRunData> quiz,
    IQuizQuestionRenderer renderer,
    CancellationToken ct)
{
    var run = await quiz.GetRequiredAsync(ct);
    await renderer.RenderCurrentQuestionAsync(ctx, run, ct);
}
```

Rules:

- State loops are not recursion. Each update must still complete normally.
- Re-enter must not push the current state into wizard history.
- Typed state data must be explicit; TeleFlow must not silently track mutable objects and auto-save them after handler execution.
- State data is a runtime cursor for conversational progress. Durable domain history belongs in application repositories or databases.
- Complex question-level machines belong in application/domain code, stored as typed state data, not as a heavy nested FSM DSL in TeleFlow core.
- Generated and reflection registration must support the same state enter and typed state data behavior.

Expected outcome:

- Dynamic quiz flows can use one outer state such as `quiz:question` with typed state data for current question, draft answer, score, attempts, and nested question step.
- Combined question flows such as "choose option + add comment" can be modeled by multiple handlers in the same outer state and a domain draft state stored in typed state data.
- Prompt rendering can live in state enter handlers instead of being duplicated after every transition.
- Wizard history remains meaningful for back navigation in linear flows.
- Documentation contains examples for simple wizards, dynamic quizzes, retry loops, pagination, and nested domain state machines.

## Handler Execution Policies

Status: planned after stable handler metadata is frozen.

Current state:

- Filters select handlers.
- Update middleware wraps the whole update pipeline.
- Telegram error handlers cover selected-handler failures.
- Handler-level rate limiting is planned as a first-class feature.

Target state:

- Add optional per-handler execution policies that wrap only the selected handler.
- Support production policies such as audit, idempotency, transaction boundaries, timeouts, metrics, feature flags, validation, and handler-level rate limits.
- Keep policy metadata generated and visible for debugging.

Rules:

- This is not AOP, aspects, or a mediator pipeline.
- Handler policies must not select another handler, continue route matching, or create a second dispatcher.
- Exceptions bubble unchanged unless an explicitly attached policy handles them intentionally.
- Policies are resolved from the per-update DI scope and execute in deterministic order.

Expected outcome:

- Method-level and module/class-level policy metadata is generated and test-covered.
- Policy ordering is documented and stable.
- Handler-level rate limits use selected handler metadata and do not run as pre-dispatch update middleware.

## Observability And OpenTelemetry

Status: planned after route, handler, module, scene, request, and error metadata are stable.

Current state:

- TeleFlow exposes logging and benchmark coverage.
- Public OpenTelemetry package does not exist.

Target state:

- Add an optional `TeleFlow.Telegram.OpenTelemetry` package.
- Expose traces for update processing, middleware, handler execution, error handling, and outgoing Telegram requests.
- Expose metrics for update counts, handler duration, Telegram request duration, retries, backoff, failures, and rate-limit decisions.
- Define stable semantic attribute names for handler, route, module, scene, update type, Telegram method, status, and exception classification.

Rules:

- OpenTelemetry must remain optional and outside the core runtime packages.
- Tokens, request bodies, response bodies, message text, callback data, file names, and other PII are not exported by default.
- Microsoft.Extensions.Logging diagnostics must stay useful without OpenTelemetry.

Expected outcome:

- Traces show update-to-handler-to-Telegram-request relationships.
- Metrics identify slow handlers and slow Telegram API calls.
- Instrumentation works for long polling and webhooks.

## Localization

Status: planned after message helpers, keyboard builders, scenes, and DI APIs are stable.

Current state:

- Handlers can use ordinary application localization services through DI.
- TeleFlow does not provide a Telegram-context localization layer.

Target state:

- Add a scoped localization boundary for Telegram contexts.
- Provide a replaceable locale resolver that can use Telegram `language_code`, user profile data, state, or application storage.
- Integrate localization with message helpers, keyboards, scenes, validation messages, and deep-link messages.

Rules:

- Localization must not change dispatch behavior.
- Missing translation behavior must be explicit and testable.
- Telegram `language_code` is a default signal, not the only source of truth.

Expected outcome:

- Handlers can access localized strings through context.
- Keyboards and scene prompts can be localized without custom boilerplate.
- Locale resolution can be replaced through DI.

## Dynamic Access Control

Status: planned as an optional module.

Current state:

- Telegram-native role checks belong to `RequireTelegramRole` and use Telegram member status.
- Bot-owned roles, ranks, permissions, and per-command overrides are not a first-class module.

Target state:

- Add an optional access-control package for runtime mutable bot-owned authorization.
- Support permissions, ranks, roles, command overrides, and scopes such as global, chat, topic/thread, business connection, and custom application scope.
- Provide replaceable storage, decision cache, subject resolver, scope resolver, and audit sink contracts.

Rules:

- Dynamic ACL is separate from Telegram-native member status.
- ACL state is bot-owned runtime data and must be mutable without redeploying handlers.
- Deny behavior must be explicit: silent no-op, typed exception, callback answer, message reply, or custom deny handler.
- ACL must not call Telegram APIs unless combined with an explicit Telegram-native filter.

Expected outcome:

- Runtime permission changes affect later updates without redeploy.
- Module-level and handler-level requirements combine deterministically.
- Access checks are testable without Telegram network access.
- Generated and explicit reflection registration produce equivalent ACL metadata.

## Distributed Runtime

Status: planned for production deployments after single-process contracts are stable.

Current state:

- Applications can replace state keys through `IStateKeyFactory`.
- Enterprise docs already recommend gateway/worker architectures for high-load bots.
- There is no first-class broker worker, distributed rate limiting, or event isolation package.

Target state:

- Add optional distributed rate limiting providers for incoming updates and outgoing Telegram requests.
- Add event isolation for stateful flows so updates for the same logical conversation can be processed in deterministic order.
- Add broker-backed gateway/worker support for durable buffering and horizontal workers.
- Provide custom partition key and deduplication key generation separately from state key generation.

Rules:

- No broker, queue, stream, Redis, Kafka, RabbitMQ, NATS, or cloud-provider dependency belongs in `TeleFlow.Core`.
- Event isolation serializes updates for a key; it must not silently drop updates.
- Broker-backed processing uses at-least-once delivery as the baseline. Exactly-once is not promised.
- Broker messages are acknowledged only after `IUpdateProcessor` succeeds.
- Cancellation must not acknowledge unfinished work.

Expected outcome:

- Users can build a webhook gateway that publishes Telegram updates to a broker.
- Users can run multiple TeleFlow workers over broker messages.
- Custom partition and deduplication keys are replaceable through DI.
- Existing `IStateKeyFactory` customization remains independent.
- Docs explain ordering, deduplication, retries, dead-letter behavior, and shutdown.

## NativeAOT And Trimming

Status: planned in stages.

Current state:

- Client-only AOT smoke verification exists as an engineering target.
- Full client request serialization AOT support depends on generated JSON metadata and avoiding runtime reflection in request serialization and multipart metadata.
- Full framework NativeAOT support is a separate track because handler reflection registration and DI invocation have different constraints.

Target state:

- Make the low-level Telegram client package trimming-friendly and client-only NativeAOT friendly first.
- Move request/response JSON serialization toward generated metadata.
- Avoid `PropertyInfo` reflection for multipart request construction by using generated method/property metadata.
- Keep framework AOT work separate from client AOT work.

Expected outcome:

- A client-only smoke app publishes with `PublishAot=true` without TeleFlow client trim/AOT warnings.
- Generated JSON metadata covers representative simple requests, multipart requests, union wrappers, and response envelopes.
- Framework docs state which registration paths are AOT-compatible.

## Schema Automation And Release Tooling

Status: planned as release hardening.

Current state:

- Telegram schema generation is maintained in a separate generator repository.
- Schema updates are expected to create reviewable changes in the main repository.
- NuGet publishing and GitHub Releases are separate release surfaces.

Target state:

- Keep automatic Telegram Bot API monitoring and PR creation for schema updates.
- Support manual forced schema regeneration for maintainer-triggered refreshes.
- Keep generated manifests carrying schema version, generator version, source hash, and generation timestamp.
- Add guardrails so schema or generator updates require an intentional package version decision.
- Keep release verification, NuGet publishing, GitHub Releases, and docs deployment explicit and auditable.

Expected outcome:

- Schema-only updates produce small reviewable diffs when Telegram API shape has not changed.
- Forced regeneration is available without pretending that Telegram API changed.
- CI catches forgotten manifest/version mismatches.
- Release notes identify Telegram Bot API schema version when relevant.

## Documentation, Templates, And Examples

Status: continuous work.

Current state:

- English and Russian documentation exist.
- README links to docs, community chat, NuGet, roadmap, and benchmark methodology.
- A realistic support-desk tutorial exists.

Target state:

- Add stable project templates for simple bots, production bots, webhook bots, and large modular bots.
- Keep docs split by audience: quickstart for beginners, deeper paths for production users, and enterprise guidance for large deployments.
- Keep generated/API reference documentation aligned with source XML comments.
- Keep examples buildable against published packages.

Expected outcome:

- New users can create and run a long-polling bot from docs in minutes.
- Production users can find package, configuration, logging, deployment, state, and versioning guidance without reading source code.
- Samples and templates are validated by CI or release verification.
