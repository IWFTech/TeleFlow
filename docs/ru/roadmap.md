# Roadmap TeleFlow

Этот документ хранит запланированную работу над framework. Он намеренно отделён от feature documentation: страницы вне roadmap описывают только API, которые уже есть в репозитории.

## Текущие правила

- Пункты roadmap не являются обещанием public API, пока они не реализованы, не покрыты тестами и не описаны в соответствующих feature pages.
- Запланированные фичи должны сохранять явную runtime-модель TeleFlow.
- Удобные API не должны прятать поведение, которое влияет на retries, rate limits, state consistency, delivery guarantees или порядок выполнения handlers.
- Enterprise-oriented фичи должны иметь заменяемое хранилище и наблюдаемое поведение при ошибках.

## Rate limiting и retry policies

### Handler-level rate limiting

Статус: запланировано.

Текущее состояние:

- TeleFlow поддерживает custom handler filters через `[UseFilter<TFilter>]`.
- `[UseFilter<TFilter>]` можно вешать на handler method или handler class.
- Custom filters уже сейчас позволяют реализовать cooldown, но это требует пользовательского кода и не даёт стандартный rejection response.
- `IUpdateRateLimiter` есть для incoming update middleware, но он выполняется до выбора handler и не является правильным финальным API для per-handler policies.

Целевое состояние:

- Добавить first-class API для rate limiting отдельных handlers и групп handlers.
- Поддержать policies: per-user, per-chat, per-user-per-chat, per-command и custom keys.
- Дать memory storage для простых ботов и заменяемый distributed storage contract для production deployments.
- Сделать явное поведение при превышении лимита: пропустить молча, ответить пользователю, бросить typed exception или вызвать настроенный rejection delegate.
- Сохранить parity между generated и reflection registration.
- Держать policy application видимой в handler metadata, чтобы debugging не зависел от скрытых runtime conventions.

Возможная форма public API:

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
            await context.Message.AnswerAsync("Подожди перед повторным использованием команды.");
        }));
});
```

Acceptance criteria:

- Method-level и class-level policies работают.
- Несколько policies на одном handler имеют детерминированный порядок.
- Generated handler metadata содержит rate limiting descriptors.
- Reflection registration и generated registration дают одинаковое route behavior.
- Rejection behavior явное и покрыто тестами.
- Cancellation уважается при ожидании storage или rejection callbacks.
- Memory storage доступен для локальных и небольших ботов.
- Distributed storage можно реализовать без замены handler dispatcher.
- Documentation содержит примеры для junior, production и enterprise usage.

Non-goals для первой реализации:

- Не делать скрытый global throttling всех Telegram API calls.
- Не делать automatic retry non-idempotent Telegram API requests сверх обработки Telegram `Retry-After`.
- Не делать implicit distributed behavior без явной регистрации storage.

### Outgoing Telegram API rate limiting

Статус: запланировано после handler-level rate limiting.

Текущее состояние:

- Telegram `429` responses с `response_parameters.retry_after` или HTTP `Retry-After` уважаются Telegram request executor.
- Обычные non-429 Telegram API failures не ретраятся автоматически.

Целевое состояние:

- Добавить explicit outgoing API rate limiting policies по bot, method, chat, user или custom key.
- Сделать retries и throttling наблюдаемыми через logs и metrics.
- Не допускать скрытые duplicate sends для non-idempotent операций.

