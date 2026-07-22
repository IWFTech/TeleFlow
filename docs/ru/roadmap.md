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
- Оставить generated assembly registration основным путем и сделать explicit registration предсказуемым.
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

Нужный результат:

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
- Automatic retry-after waiting ограничен `TelegramRetryAfterPolicy` и описан на странице Telegram client feature.
- Обычные non-429 Telegram API failures не ретраятся автоматически.

Целевое состояние:

- Добавить explicit outgoing API rate limiting policies по bot, method, chat, user или custom key.
- Сделать retries и throttling наблюдаемыми через logs и metrics.
- Не допускать скрытые duplicate sends для non-idempotent операций.

## Готовность к stable release

Статус: запланировано до первого stable release.

Текущее состояние:

- У TeleFlow есть публичные alpha packages, generated Telegram client methods, handler routing, state/wizard APIs, callbacks, long polling, webhooks, error handlers, документация, release workflows и no-network benchmarks.
- Package boundaries явные: client, schema, framework, raw transports и framework transport adapters живут в отдельных packages.
- Public APIs ещё могут меняться до первого stable release.

Целевое состояние:

- Проверить и заморозить public API names, package graph, handler metadata, diagnostics и registration behavior.
- Удалить или явно пометить transitional APIs до публикации stable packages.
- Держать `TeleFlow.Framework.Core` transport-agnostic, generated schema DTOs свободными от framework behavior, а framework runtime behavior внутри Telegram framework packages.
- Оставить generated assembly registration рекомендуемым production path, а deprecated reflection assembly registration поставить на путь удаления до `1.0`.
- Синхронизировать quickstart, package guide, enterprise docs, samples и release notes с реальными packages и APIs.

Нужный результат:

- CI, release verification, package graph tests, analyzer tests и representative sample builds зелёные.
- Generated registration является production assembly path, а explicit handler/module registration имеет documented semantics.
- Документация описывает текущее поведение; planned features остаются только в roadmap.
- Release notes фиксируют alpha/stable status, breaking-change rules, supported .NET versions и Telegram Bot API schema version.

## Startup и delivery controls

Статус: запланировано.

Текущее состояние:

- Long polling сохраняет at-least-once update processing: offset двигается только после успешной обработки update.
- Public docs не заявляют `drop_pending_updates` option.
- Deep-link URL building использует явную настройку bot username и не вызывает `getMe` лениво.

Целевое состояние:

- Добавить явные startup delivery controls для long polling, включая документированный аналог `drop_pending_updates` с точными offset и acknowledgement semantics.
- Оценить opt-in startup bot identity resolver, который может один раз вызвать `getMe`, закэшировать bot identity и fail/warn по явной конфигурации.
- Не добавлять lazy network calls в helpers вроде deep-link builders.

Нужный результат:

- Pending-update behavior покрыт тестами и описан для long polling startup.
- Startup identity resolution имеет детерминированное failure behavior.
- Bot identity lookup не происходит неявно во время handler execution или deep-link URL construction.

## State flow ergonomics

Статус: запланировано после стабилизации текущих state и wizard contracts.

Текущее состояние:

- TeleFlow поддерживает явный current state через `ctx.State`.
- TeleFlow поддерживает state data через `ctx.State.Data` и `ctx.Wizard.Data`.
- State data сейчас достаётся по string keys вроде `"quiz"`.
- `Wizard.GoToAsync(state)` записывает navigation history и лучше всего подходит для линейных flows и явного back navigation.
- Dynamic loops уже можно реализовать: оставаться в том же state, обновлять state data и вручную рендерить следующий prompt.
- First-class state enter hook и явный re-enter/self-transition API пока отсутствуют.

Целевое состояние:

- Добавить typed state data helpers, чтобы common flow data можно было использовать без повторения string keys.
- Оценить явный handler parameter `StateData<T>`, который показывает, что данные пришли из state storage, а не из DI, route values или callback payload.
- Добавить state enter hooks для prompts, которые должны выполняться при входе в state.
- Добавить явный re-enter/self-transition API, который вызывает state enter behavior без загрязнения wizard history.
- Описать dynamic state-loop patterns для quizzes, paginated menus, retry-until-valid-input flows и long-running support workflows.

Возможная форма public API:

```csharp
[Message]
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
        await ctx.Message.AnswerAsync("Опросник завершён.", ct);
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

Правила:

- State loops - это не recursion. Каждый update всё равно должен завершаться обычным образом.
- Re-enter не должен класть current state в wizard history.
- Typed state data должен быть явным; TeleFlow не должен скрыто отслеживать mutable objects и auto-save после handler execution.
- State data - это runtime cursor для conversational progress. Durable domain history должна жить в application repositories или databases.
- Complex question-level machines должны жить в application/domain code и храниться как typed state data, а не превращаться в тяжёлый nested FSM DSL внутри TeleFlow core.
- Generated registration и explicit handler/module registration должны поддерживать documented state enter и typed state data behavior.

Нужный результат:

- Dynamic quiz flows смогут использовать один outer state вроде `quiz:question` и typed state data для current question, draft answer, score, attempts и nested question step.
- Combined question flows вроде "выбрать вариант + добавить комментарий" можно будет моделировать несколькими handlers в одном outer state и domain draft state внутри typed state data.
- Prompt rendering сможет жить в state enter handlers, а не дублироваться после каждого transition.
- Wizard history останется осмысленной для back navigation в линейных flows.
- Documentation содержит примеры simple wizards, dynamic quizzes, retry loops, pagination и nested domain state machines.

## Handler execution policies

Статус: запланировано после стабилизации handler metadata.

Текущее состояние:

- Filters выбирают handlers.
- Update middleware оборачивает весь update pipeline.
- Telegram error handlers покрывают selected-handler failures.
- Handler-level rate limiting запланирован как first-class feature.

Целевое состояние:

- Добавить optional per-handler execution policies, которые оборачивают только выбранный handler.
- Поддержать production policies: audit, idempotency, transaction boundaries, timeouts, metrics, feature flags, validation и handler-level rate limits.
- Держать policy metadata generated и видимой для debugging.

Правила:

- Это не AOP, не aspects и не mediator pipeline.
- Handler policies не выбирают другой handler, не продолжают route matching и не создают второй dispatcher.
- Exceptions bubble unchanged, если явно подключённая policy не обработала их намеренно.
- Policies резолвятся из per-update DI scope и выполняются в детерминированном порядке.

Нужный результат:

- Method-level и module/class-level policy metadata generated и покрыты тестами.
- Policy ordering задокументирован и стабилен.
- Handler-level rate limits используют selected handler metadata и не работают как pre-dispatch update middleware.

## Observability и OpenTelemetry

Статус: запланировано после стабилизации route, handler, module, scene, request и error metadata.

Текущее состояние:

- TeleFlow уже даёт logging и benchmark coverage.
- Public OpenTelemetry package отсутствует.

Целевое состояние:

- Добавить optional `TeleFlow.Telegram.OpenTelemetry` package.
- Дать traces для update processing, middleware, handler execution, error handling и outgoing Telegram requests.
- Дать metrics для update counts, handler duration, Telegram request duration, retries, backoff, failures и rate-limit decisions.
- Зафиксировать stable semantic attribute names для handler, route, module, scene, update type, Telegram method, status и exception classification.

Правила:

- OpenTelemetry должен остаться optional и вне core runtime packages.
- Tokens, request bodies, response bodies, message text, callback data, file names и другие PII не экспортируются по умолчанию.
- Microsoft.Extensions.Logging diagnostics остаются полезными без OpenTelemetry.

Нужный результат:

- Traces показывают связь update-to-handler-to-Telegram-request.
- Metrics помогают находить slow handlers и slow Telegram API calls.
- Instrumentation работает для long polling и webhooks.

## Localization

Статус: реализовано для `1.0.0-alpha.13` в optional packages.

Текущее состояние:

- `IWF.TeleFlow.Framework.I18n` один раз на Telegram update определяет scoped locale через ordered application resolvers, Telegram `language_code` и fallback.
- `IWF.TeleFlow.Framework.I18n.Fluent` загружает immutable Fluent catalogs во время startup validation и даёт plain, HTML, MarkdownV2 и explicit-locale formatting.
- Handlers, generated method parameters, middleware, background services, keyboards, callback responses, custom emoji и rich-message LaTeX используют одни и те же небольшие contracts.

Правила:

- Localization не меняет dispatch behavior.
- Missing translation behavior должно быть явным и тестируемым.
- Telegram `language_code` - default signal, но не единственный source of truth.

Будущая работа:

- Рассматривать hot reload только после реального production use case и определения consistency semantics.
- Рассмотреть application-defined pure Fluent functions без утечки Linguini types в public contracts TeleFlow.

## Dynamic access control

Статус: запланировано как optional module.

Текущее состояние:

- Telegram-native role checks относятся к `RequireTelegramRole` и используют Telegram member status.
- Bot-owned roles, ranks, permissions и per-command overrides пока не являются first-class module.

Целевое состояние:

- Добавить optional access-control package для runtime mutable bot-owned authorization.
- Поддержать permissions, ranks, roles, command overrides и scopes: global, chat, topic/thread, business connection и custom application scope.
- Дать replaceable storage, decision cache, subject resolver, scope resolver и audit sink contracts.

Правила:

- Dynamic ACL отделён от Telegram-native member status.
- ACL state - bot-owned runtime data, который меняется без redeploy handlers.
- Deny behavior явный: silent no-op, typed exception, callback answer, message reply или custom deny handler.
- ACL не вызывает Telegram APIs, если он не объединён с explicit Telegram-native filter.

Нужный результат:

- Runtime permission changes влияют на следующие updates без redeploy.
- Module-level и handler-level requirements combine deterministically.
- Access checks тестируются без Telegram network access.
- Generated registration и explicit handler/module registration дают documented ACL metadata.

## Distributed runtime

Статус: запланировано для production deployments после стабилизации single-process contracts.

Текущее состояние:

- Applications могут заменить state keys через `IStateKeyFactory`.
- Enterprise docs уже рекомендуют gateway/worker architectures для high-load bots.
- First-class broker worker, distributed rate limiting и event isolation package пока нет.

Целевое состояние:

- Добавить optional distributed rate limiting providers для incoming updates и outgoing Telegram requests.
- Добавить event isolation для stateful flows, чтобы updates одного logical conversation обрабатывались в детерминированном порядке.
- Добавить broker-backed gateway/worker support для durable buffering и horizontal workers.
- Дать custom partition key и deduplication key generation отдельно от state key generation.

Правила:

- Broker, queue, stream, Redis, Kafka, RabbitMQ, NATS или cloud-provider dependency не попадают в `TeleFlow.Framework.Core`.
- Event isolation сериализует updates по key; он не должен молча drop updates.
- Broker-backed processing использует at-least-once delivery как baseline. Exactly-once не обещается.
- Broker messages acknowledge только после успешного `IUpdateProcessor`.
- Cancellation не acknowledge unfinished work.

Нужный результат:

- Users могут собрать webhook gateway, который публикует Telegram updates в broker.
- Users могут запускать несколько TeleFlow workers поверх broker messages.
- Custom partition и deduplication keys заменяются через DI.
- Существующий `IStateKeyFactory` остаётся независимым.
- Docs объясняют ordering, deduplication, retries, dead-letter behavior и shutdown.

## NativeAOT и trimming

Статус: запланировано поэтапно.

Текущее состояние:

- Client-only AOT smoke verification существует как engineering target.
- Full client request serialization AOT support зависит от generated JSON metadata и отказа от runtime reflection в request serialization и multipart metadata.
- Full framework NativeAOT support - отдельный track, потому что handler registration и DI invocation имеют разные constraints.

Целевое состояние:

- Сначала сделать low-level Telegram client package trimming-friendly и client-only NativeAOT friendly.
- Перевести request/response JSON serialization к generated metadata.
- Убрать `PropertyInfo` reflection из multipart request construction через generated method/property metadata.
- Держать framework AOT отдельно от client AOT.

Нужный результат:

- Client-only smoke app публикуется с `PublishAot=true` без TeleFlow client trim/AOT warnings.
- Generated JSON metadata покрывает representative simple requests, multipart requests, union wrappers и response envelopes.
- Framework docs фиксируют, какие registration paths AOT-compatible.

## Schema automation и release tooling

Статус: запланировано как release hardening.

Текущее состояние:

- Telegram schema generation поддерживается в отдельной generator repository.
- Schema updates должны создавать reviewable changes в main repository.
- NuGet publishing и GitHub Releases являются разными release surfaces.

Целевое состояние:

- Сохранить automatic Telegram Bot API monitoring и PR creation для schema updates.
- Поддержать manual forced schema regeneration для maintainer-triggered refresh.
- Держать generated manifests со schema version, generator version, source hash и generation timestamp.
- Добавить guardrails, чтобы schema или generator updates требовали intentional package version decision.
- Держать release verification, NuGet publishing, GitHub Releases и docs deployment явными и auditable.

Нужный результат:

- Schema-only updates дают маленькие reviewable diffs, когда Telegram API shape не поменялся.
- Forced regeneration доступна без имитации Telegram API change.
- CI ловит forgotten manifest/version mismatches.
- Release notes указывают Telegram Bot API schema version, когда это relevant.

## Documentation, templates и examples

Статус: постоянная работа.

Текущее состояние:

- English и Russian documentation существуют.
- README ведёт на docs, community chat, NuGet, roadmap и benchmark methodology.
- Реалистичный support-desk tutorial существует.

Целевое состояние:

- Добавить stable project templates для simple bots, production bots, webhook bots и large modular bots.
- Держать docs split by audience: quickstart для beginners, deeper paths для production users и enterprise guidance для large deployments.
- Держать generated/API reference documentation aligned with source XML comments.
- Держать examples buildable against published packages.

Нужный результат:

- New users создают и запускают long-polling bot по docs за минуты.
- Production users находят package, configuration, logging, deployment, state и versioning guidance без чтения source code.
- Samples и templates валидируются CI или release verification.
