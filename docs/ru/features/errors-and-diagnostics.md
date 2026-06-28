# Ошибки и диагностика

В TeleFlow есть два слоя error handling:

- core middleware для update pipeline failures;
- Telegram error handlers для handler-level exceptions.

## Error handlers

Используй `[Error]` как catch-all handler:

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

Используй `[Error<TException>]` для конкретного exception type:

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

Error handlers могут получать context, exception, cancellation token, route values и services.

Выбор error handler детерминированный:

- module-local error handlers вызываются раньше global error handlers;
- handlers с более конкретным exception type идут раньше broad handlers;
- registration order используется для одинаковых кандидатов;
- `TelegramErrorHandlingResult.Handled` останавливает обработку ошибки;
- `TelegramErrorHandlingResult.Unhandled` позволяет TeleFlow попробовать следующий compatible error handler.

Если ни один compatible error handler не вернул `Handled`, исходное exception выбрасывается дальше.

## Не скрывай unknown errors

Не превращай каждое exception в user-facing message. В production systems стоит обрабатывать known application errors, а unknown failures оставлять observable через logs и hosting infrastructure.

Хорошие кандидаты для handler-level recovery:

- validation failures;
- missing business entities;
- rejected user actions;
- known external service responses.

Плохие кандидаты:

- corrupted state;
- programmer errors;
- misconfiguration;
- failed invariants.

## Update exception middleware

Core exception middleware логирует update pipeline failures и rethrows. Это оставляет failures видимыми для host.

## Logging

TeleFlow использует `Microsoft.Extensions.Logging`. Регистрируй logging через hosting model приложения:

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
});
```

Для enterprise deployments отправляй logs в существующую platform и держи update id, chat id, handler и exception type searchable.

## Логи времени выполнения обработчиков

Подробные замеры времени выполнения обработчиков собираются только когда для логгера Telegram framework включён `LogLevel.Debug`. В логах уровня Debug TeleFlow может показать общее время обработчика, количество Telegram-запросов, время Telegram-запросов и время собственной логики обработчика.

Когда Debug logging выключен, обычный успешный путь обработки не создаёт области замера Telegram-запросов. Логи ошибок по-прежнему содержат update id, update type, handler, route, module, scene и exception type, но не содержат подробные поля времени, которые не собирались.

## Рекомендации по диагностике

Начни с:

- console logging during development;
- structured logs in production;
- explicit error handlers для known business exceptions;
- tests для error handlers, которые меняют user-visible behavior.

Не скрывай unknown exceptions, если нет конкретной operational reason и другого observable signal.
