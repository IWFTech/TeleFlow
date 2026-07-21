# Update Failure And Delivery Contract

This page describes what TeleFlow does after an incoming update reaches the
framework, which failures acknowledge that update, and which failures are
deliberately returned to the hosting environment.

The short version is simple: TeleFlow acknowledges an update only after its
processing has reached an explicit successful outcome. It does not guess that
failed business work is safe to discard.

## Why This Contract Exists

Telegram can deliver an update again when the previous delivery was not
acknowledged. That is useful: a temporary process failure should not silently
lose a user action. It also means that retrying an update after a handler has
already changed a database, charged a balance, or sent a message can repeat a
side effect.

TeleFlow therefore provides **at-least-once delivery**, not exactly-once
delivery. Applications that modify durable state must make their own business
operations idempotent where repeated delivery is possible.

## Framework Pipeline

For a framework update, the pipeline is:

1. The transport receives an update.
2. `IUpdateProcessor` creates one scoped `UpdateContext` and runs update
   middleware.
3. The Telegram dispatcher selects a route and invokes its handler.
4. For a callback route, TeleFlow runs the configured automatic callback answer
   after the handler succeeds.
5. If route execution fails, compatible `[Error]` and `[Error<TException>]`
   handlers are tried.
6. The transport acknowledges the update only after the processor completes
   successfully.

The selected handler and the automatic callback answer are one **route
execution boundary**. This matters because an automatic callback answer is a
framework action that happens after the handler, but it must not bypass the
same recovery decision that applies to the selected route.

## Outcome Matrix

| Outcome | Framework result | Long polling | Webhook |
| --- | --- | --- | --- |
| No route matches the update | Normal completion | Offset advances | `200 OK` |
| A rate limiter intentionally rejects the update | Normal completion | Offset advances | `200 OK` |
| Handler and automatic callback answer succeed | Normal completion | Offset advances | `200 OK` |
| An `[Error]` handler returns `Handled` | Normal completion | Offset advances | `200 OK` |
| An error handler returns `Unhandled` and no later handler handles the error | Failure propagates | Offset does not advance | Endpoint failure propagates |
| Handler, middleware, or error handler throws | Failure propagates | Offset does not advance | Endpoint failure propagates |
| Application cancellation | Processing stops | Offset does not advance | Cancellation semantics apply |

For webhooks, TeleFlow does not manufacture a success response after an
unhandled failure. ASP.NET Core returns the failure response and Telegram's
delivery mechanism decides whether to redeliver it.

## Error Handlers Are The Explicit Recovery Point

Use an error handler when the application has made a deliberate recovery
decision. Returning `Handled` means: *this update reached a durable and
acceptable outcome; the transport may acknowledge it.*

```csharp
public sealed class KnownErrors
{
    [Error<RejectedUserActionException>]
    public async Task<TelegramErrorHandlingResult> RejectedAction(
        MessageContext ctx,
        RejectedUserActionException exception,
        CancellationToken ct)
    {
        await ctx.Message.AnswerAsync(exception.Message, ct);
        return TelegramErrorHandlingResult.Handled;
    }
}
```

Returning `Unhandled` asks TeleFlow to try the next compatible error handler.
Throwing from an error handler means recovery failed; that new failure remains
visible to the transport and host.

Do not register a catch-all `[Error]` handler that returns `Handled` for every
exception. That turns defects, failed invariants, and broken configuration into
acknowledged updates with no reliable recovery path.

## Telegram API Errors

An outgoing Telegram request made inside a handler belongs to that handler's
route execution. Its failure can be handled by a compatible error handler, or
it can remain unhandled and preserve at-least-once delivery.

Do not classify every `TelegramBadRequestException` as harmless. A `400` may
mean an expected no-op such as an unchanged message, but it can also mean
invalid markup, an invalid chat, or an application bug. Prefer one of these
paths:

- prevent known no-op requests before sending them;
- translate a known application condition into a domain exception and handle
  that exception explicitly;
- let an unknown Telegram request failure remain observable and unacknowledged.

Telegram request retry is also intentionally narrow. The client automatically
honours bounded `429 retry_after` responses. Raw `getUpdates` has its own
transient-failure backoff because it is an idempotent read. TeleFlow does not
blindly retry all outgoing `network` or `5xx` failures: Telegram may have
already completed a write before the client lost the response.

## Middleware Is A Different Boundary

Update middleware runs before or around dispatch. A middleware can intentionally
stop an update by not calling `next(context)`; rate-limit rejection uses that
model and completes normally.

An exception thrown by middleware is not a selected Telegram route failure, so
it does not enter `[Error]` handlers. This is intentional. Middleware often
owns authentication, storage transactions, tenancy, or global safeguards; a
handler-level recovery decision cannot safely claim those failures succeeded.

Catch and resolve a middleware failure inside that middleware only when the
middleware itself can make a durable, explicit decision.

## Automatic Callback Answers

Automatic callback answers are opt-in. A callback handler does not send
`answerCallbackQuery` unless it has `[AutoAnswerCallback]` (on the method or
handler type), or the application explicitly registers
`AddAutoCallbackAnswer(...)`. When neither is configured, TeleFlow sends no
automatic callback answer.

`[AutoAnswerCallback]` and `AddAutoCallbackAnswer(...)` run only after the
selected callback handler completes successfully. If the automatic
`answerCallbackQuery` call fails, TeleFlow uses the same `[Error]` pipeline as
for the selected route.

This avoids a separate hidden rule for an automatic framework action:

- an application can return `Handled` when it has explicitly accepted the
  outcome;
- otherwise the failure stays unhandled and the update is not acknowledged;
- no automatic callback-answer failure is silently swallowed.

## Production Guidance

Keep handlers thin and move durable state changes into application services
with clear idempotency rules. In particular:

- persist a deduplication key before performing irreversible work when the
  business operation requires it;
- use database transactions or an outbox when state and external effects must
  stay coordinated;
- handle known user-facing rejections explicitly;
- let unknown failures reach logs, telemetry, and the host;
- use a process supervisor for long-polling workers, but do not treat restart
  as a business retry policy.

This is more work than acknowledging every exception, but it is the difference
between a visible failure and silently losing a user action.

## Related Pages

- [Errors and diagnostics](../features/errors-and-diagnostics.md)
- [Long polling](../transports/long-polling.md)
- [Webhooks](../transports/webhooks.md)
- [Middleware and rate limiting](../advanced/middleware-and-rate-limiting.md)
