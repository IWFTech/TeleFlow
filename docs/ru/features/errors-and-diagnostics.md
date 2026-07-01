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

Для enterprise deployments отправляй logs в существующую platform и держи update id, update type, handler, route, method name, HTTP status и exception type searchable.

Event id в TeleFlow стабильны внутри logger category. Диагностический контракт - это category, event id, event name и message template. Event id не являются глобальным числовым registry по всем TeleFlow assemblies.

## Уровни логов

TeleFlow использует log levels осознанно:

| Level | Meaning |
| --- | --- |
| `Information` | Runtime lifecycle events: polling start, connected, stopped и coarse transport status. |
| `Debug` | Per-update diagnostics: handler matching, handler completion, route misses, filter rejections, webhook update acceptance и request timings. |
| `Warning` | Recoverable или ожидаемые runtime rejections: retry-after, invalid webhook secret, invalid webhook payload, callback payload decode failure и rate-limit rejection. |
| `Error` | Unhandled handler failures, Telegram request failures, webhook processing failures и update processing failures. |

Обычная успешная обработка update не логируется на `Information`.

## Логи времени выполнения handlers

Подробные замеры времени выполнения обработчиков собираются только когда для логгера Telegram framework включён `LogLevel.Debug`. В логах уровня Debug TeleFlow может показать общее время обработчика, количество Telegram-запросов, время Telegram-запросов и время собственной логики обработчика.

Когда Debug logging выключен, обычный успешный путь обработки не создаёт области замера Telegram-запросов. Логи ошибок по-прежнему содержат update id, update type, handler, route, module, scene и exception type, но не содержат подробные поля времени, которые не собирались.

## Security и privacy

TeleFlow не является logging engine. Он отправляет diagnostic events через application-owned `Microsoft.Extensions.Logging` providers.

Framework logs используют статические framework-owned message templates. TeleFlow не исполняет, не evaluate-ит и не делает lookup operations над log content, а user-controlled strings не используются как logging templates.

По умолчанию framework logs не содержат:

- bot tokens;
- webhook secrets;
- raw callback data;
- request bodies;
- message text;
- message captions;
- arbitrary rate-limit keys;
- arbitrary user-provided values.

Безопасные metadata: update id, update type, handler, route, module, scene, exception type, HTTP status code, Telegram method name, retry-after, limiter type и developer-controlled policy name.

Logging providers остаются infrastructure приложения. Если приложение подключает provider или sink со своими unsafe processing rules, это находится вне runtime contract TeleFlow.

## Rate-limit diagnostics

Update-level rate limiters возвращают `UpdateRateLimitDecision`. Rejected decision останавливает pipeline и логируется как `Warning`; это не exception.

Failures внутри limiter-а всё ещё выбрасываются как обычные exceptions и видны через regular error path.

## Рекомендации по диагностике

Начни с:

- console logging during development;
- structured logs in production;
- `Debug` logging только во время local debugging или targeted investigations;
- explicit error handlers для known business exceptions;
- tests для error handlers, которые меняют user-visible behavior.

Не скрывай unknown exceptions, если нет конкретной operational reason и другого observable signal.
