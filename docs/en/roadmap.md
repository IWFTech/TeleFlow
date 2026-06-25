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

Acceptance criteria:

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

