# Контракт Ошибок И Подтверждения Update

Эта страница описывает, что происходит после того, как incoming update попал
во framework: какие исходы подтверждают update, а какие намеренно возвращаются
в hosting environment как ошибка.

Коротко: TeleFlow подтверждает update только после явного успешного исхода. Он
не пытается угадать, что упавшая бизнес-операция безопасна для игнорирования.

## Зачем Нужен Этот Контракт

Telegram может повторно доставить update, если предыдущая доставка не была
подтверждена. Это полезно: временное падение процесса не должно молча потерять
действие пользователя. Но если handler уже изменил базу, списал баланс или
отправил сообщение, повторная доставка может повторить side effect.

Поэтому TeleFlow даёт **at-least-once delivery**, а не exactly-once delivery.
Приложение, меняющее durable state, само отвечает за idempotency бизнес-операций
в местах, где возможна повторная доставка.

## Pipeline Framework

Для framework update путь выглядит так:

1. Transport получает update.
2. `IUpdateProcessor` создаёт scoped `UpdateContext` и запускает update
   middleware.
3. Telegram dispatcher выбирает route и вызывает его handler.
4. Для callback route TeleFlow после успешного handler запускает настроенный
   automatic callback answer.
5. Если route execution упал, вызываются compatible `[Error]` и
   `[Error<TException>]` handlers.
6. Transport подтверждает update только после успешного завершения processor.

Выбранный handler и automatic callback answer образуют одну границу **route
execution**. Auto-answer выполняется после handler, но это не повод обходить
то же recovery-решение, которое действует для выбранного route.

## Матрица Исходов

| Исход | Результат framework | Long polling | Webhook |
| --- | --- | --- | --- |
| Для update нет подходящего route | Нормальное завершение | Offset продвигается | `200 OK` |
| Rate limiter намеренно отклонил update | Нормальное завершение | Offset продвигается | `200 OK` |
| Handler и automatic callback answer успешно завершились | Нормальное завершение | Offset продвигается | `200 OK` |
| `[Error]` handler вернул `Handled` | Нормальное завершение | Offset продвигается | `200 OK` |
| Error handler вернул `Unhandled`, и дальше никто не обработал ошибку | Ошибка идёт выше | Offset не продвигается | Ошибка endpoint идёт выше |
| Handler, middleware или error handler бросил exception | Ошибка идёт выше | Offset не продвигается | Ошибка endpoint идёт выше |
| Application cancellation | Обработка останавливается | Offset не продвигается | Работает cancellation semantics |

Для webhook TeleFlow не подделывает успешный response после unhandled failure.
ASP.NET Core возвращает failure response, а решение о повторной доставке остаётся
у механизма доставки Telegram.

## Error Handlers Это Явная Точка Recovery

Используй error handler, когда приложение действительно приняло решение о
recovery. Возврат `Handled` означает: *этот update дошёл до durable и
приемлемого результата, transport может его подтвердить.*

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

`Unhandled` просит TeleFlow попробовать следующий compatible error handler.
Если error handler сам бросил exception, recovery не удался: новая ошибка
остаётся видимой transport-у и host-у.

Не регистрируй catch-all `[Error]`, который возвращает `Handled` на любой
exception. Иначе defects, failed invariants и сломанная конфигурация превратятся
в подтверждённые updates без честного пути восстановления.

## Ошибки Telegram API

Outgoing Telegram request, сделанный внутри handler, является частью route
execution. Его ошибку можно обработать compatible error handler-ом или оставить
unhandled, сохранив at-least-once delivery.

Не считай любой `TelegramBadRequestException` безопасным. `400` может означать
ожидаемый no-op вроде unchanged message, но может говорить о невалидной
разметке, неверном chat или баге приложения. Предпочитай один из путей:

- предотврати известный no-op до отправки запроса;
- переведи известное состояние приложения в domain exception и обработай именно
  его;
- оставь неизвестную ошибку Telegram request видимой и неподтверждённой.

Retry Telegram requests тоже намеренно узкий. Client автоматически учитывает
bounded `429 retry_after`. Raw `getUpdates` имеет собственный transient backoff,
потому что это idempotent read. TeleFlow не retry-ит вслепую все outgoing
`network` или `5xx` failures: Telegram мог уже выполнить write до того, как
client потерял response.

## Middleware Это Другая Граница

Update middleware выполняется до dispatch или вокруг него. Middleware может
намеренно остановить update, не вызвав `next(context)`; так работает rate-limit
rejection, и это нормальное завершение.

Exception из middleware не является ошибкой выбранного Telegram route, поэтому
не попадает в `[Error]` handlers. Это намеренно. Middleware часто владеет
authentication, storage transactions, tenancy или глобальными safeguards;
handler-level recovery не может честно заявить, что такая ошибка успешно
завершилась.

Лови и решай ошибку внутри middleware только когда само middleware может принять
durable и явное решение.

## Automatic Callback Answers

Automatic callback answer включается только явно. Обычный callback handler не
посылает `answerCallbackQuery`, если на методе или типе нет
`[AutoAnswerCallback]` и приложение не зарегистрировало
`AddAutoCallbackAnswer(...)`. Если не задано ни одно из этих двух условий,
TeleFlow не отправляет автоматический callback answer.

`[AutoAnswerCallback]` и `AddAutoCallbackAnswer(...)` работают только после
успешного выбранного callback handler. Если автоматический `answerCallbackQuery`
падает, TeleFlow использует тот же `[Error]` pipeline, что и для выбранного
route.

Так не появляется отдельное скрытое правило для framework action:

- приложение может вернуть `Handled`, когда явно принимает этот исход;
- иначе ошибка остаётся unhandled, и update не подтверждается;
- ошибка automatic callback answer не проглатывается молча.

## Рекомендации Для Production

Держи handlers тонкими, а durable state changes выноси в application services с
явными idempotency rules. В частности:

- сохраняй deduplication key до необратимой работы, когда это нужно бизнесу;
- используй database transaction или outbox, когда state и external effects
  должны оставаться согласованными;
- явно обрабатывай известные user-facing rejections;
- оставляй неизвестные failures логам, telemetry и host-у;
- используй process supervisor для long-polling worker, но не считай restart
  бизнесовой retry policy.

Это сложнее, чем подтверждать каждый exception, но именно так не теряются
действия пользователя молча.

## Связанные Страницы

- [Errors and diagnostics](../features/errors-and-diagnostics.md)
- [Long polling](../transports/long-polling.md)
- [Webhooks](../transports/webhooks.md)
- [Middleware and rate limiting](../advanced/middleware-and-rate-limiting.md)
