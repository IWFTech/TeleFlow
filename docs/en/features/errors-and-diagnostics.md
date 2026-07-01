# Errors And Diagnostics

TeleFlow has two layers of error handling:

- core middleware for update pipeline failures;
- Telegram error handlers for handler-level exceptions.

## Error Handlers

Use `[Error]` for a catch-all handler:

```csharp
public sealed class ErrorHandlers
{
    [Error]
    public async Task<TelegramErrorHandlingResult> Any(
        TelegramErrorContext error,
        MessageContext ctx,
        Exception exception,
        ILogger<ErrorHandlers> logger,
        CancellationToken ct)
    {
        logger.LogError(exception, "Telegram handler failed.");
        await ctx.Message.AnswerAsync("Something went wrong.", ct);
        return TelegramErrorHandlingResult.Handled;
    }
}
```

Use `[Error<TException>]` for a specific exception type:

```csharp
[Error<ValidationException>]
public async Task<TelegramErrorHandlingResult> Validation(
    TelegramErrorContext error,
    MessageContext ctx,
    ValidationException exception,
    CancellationToken ct)
{
    await ctx.Message.AnswerAsync(exception.Message, ct);
    return TelegramErrorHandlingResult.Handled;
}
```

Error handlers can receive context, exception, cancellation token, route values, and services.

Error handler selection is deterministic:

- module-local error handlers are tried before global error handlers;
- handlers with a more specific exception type are tried before broader handlers;
- registration order breaks ties;
- returning `TelegramErrorHandlingResult.Handled` stops error processing;
- returning `TelegramErrorHandlingResult.Unhandled` lets TeleFlow try the next compatible error handler.

If no compatible error handler returns `Handled`, the original exception is rethrown.

## Let Unknown Errors Fail

Do not turn every exception into a user-facing message. For production systems, handle known application errors and let unknown failures remain observable through logs and hosting infrastructure.

Good candidates for handler-level recovery:

- validation failures;
- missing business entities;
- rejected user actions;
- known external service responses.

Bad candidates:

- corrupted state;
- programmer errors;
- misconfiguration;
- failed invariants.

## Update Exception Middleware

Core exception middleware logs update pipeline failures and rethrows. This keeps failures visible to the host.

## Logging

TeleFlow uses `Microsoft.Extensions.Logging`. Register logging through the hosting model you use:

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
});
```

For enterprise deployments, route logs to your existing platform and keep update id, update type, handler, route, method name, HTTP status, and exception type searchable.

TeleFlow event ids are stable within a logger category. Treat category, event id, event name, and message template as the diagnostic contract. Event ids are not a global numeric registry across every TeleFlow assembly.

## Log Levels

TeleFlow uses log levels deliberately:

| Level | Meaning |
| --- | --- |
| `Information` | Runtime lifecycle events such as polling start, connected, stopped, and coarse transport status. |
| `Debug` | Per-update diagnostics: handler matching, handler completion, route misses, filter rejections, webhook update acceptance, and request timings. |
| `Warning` | Recoverable or expected runtime rejections: retry-after, invalid webhook secret, invalid webhook payload, callback payload decode failure, and rate-limit rejection. |
| `Error` | Unhandled handler failures, Telegram request failures, webhook processing failures, and update processing failures. |

Normal successful update processing is not logged at `Information`.

## Handler Timing

Detailed handler timing is collected only when `LogLevel.Debug` is enabled for the Telegram framework logger. In Debug logs TeleFlow can report handler duration, Telegram request count, Telegram request time, and handler logic time.

When Debug logging is disabled, the normal successful handler path does not create handler request timing scopes. Error logs still include update id, update type, handler, route, module, scene, and exception type, but they do not include detailed timing fields that were not collected.

## Security And Privacy

TeleFlow is not a logging engine. It emits diagnostic events through application-owned `Microsoft.Extensions.Logging` providers.

Framework logs use static framework-owned message templates. TeleFlow does not execute, evaluate, or perform lookup operations on log content, and user-controlled strings are not used as logging templates.

By default, framework logs do not include:

- bot tokens;
- webhook secrets;
- raw callback data;
- request bodies;
- message text;
- message captions;
- arbitrary rate-limit keys;
- arbitrary user-provided values.

Safe metadata can include update id, update type, handler, route, module, scene, exception type, HTTP status code, Telegram method name, retry-after, limiter type, and developer-controlled policy name.

Logging providers remain application infrastructure. If an application configures a provider or sink with its own unsafe processing rules, that is outside TeleFlow's runtime contract.

## Rate-Limit Diagnostics

Update-level rate limiters return `UpdateRateLimitDecision`. A rejected decision stops the pipeline and logs a `Warning`; it is not treated as an exception.

Limiter failures still throw normal exceptions and remain visible through the regular error path.

## Diagnostics Recommendation

Start with:

- console logging during development;
- structured logs in production;
- `Debug` logging only during local debugging or targeted investigations;
- explicit error handlers for known business exceptions;
- tests for error handlers that change user-visible behavior.

Do not hide unknown exceptions unless there is a concrete operational reason and another observable signal.
