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

For enterprise deployments, route logs to your existing platform and keep update id, chat id, handler, and exception type searchable.

## Diagnostics Recommendation

Start with:

- console logging during development;
- structured logs in production;
- explicit error handlers for known business exceptions;
- tests for error handlers that change user-visible behavior.

Do not hide unknown exceptions unless there is a concrete operational reason and another observable signal.
