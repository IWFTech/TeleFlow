# Production checklist

Используй этот checklist перед production запуском TeleFlow bot.

Связанные страницы:

- [Конфигурация и секреты](../getting-started/configuration.md)
- [Deployment](deployment.md)
- [Custom storage](../advanced/custom-storage.md)

## Пакеты

- Используй narrow package set, который реально нужен app.
- Держи `IWF.TeleFlow.Generators` private через `PrivateAssets="all"`.
- Не ставь raw transports и framework transports вместе, если оба не используются осознанно.

## Регистрация

- Use generated assembly registration by default.
- Держи хотя бы один startup test, который проверяет generated registration.
- Direct registration используй для узких tests.
- Избегай reflection registration, если он не documented.

## Транспорт

- Выбери long polling или webhooks осознанно.
- Не регистрируй больше одного `IUpdateSource`.
- Для webhooks используй HTTPS и secret token.
- Для long polling запускай worker под host с restart policy.
- Задокументируй, что делать с pending updates после downtime.

## Хранилище

- Не используй memory state storage для multi-instance production.
- Определи state key structure.
- Определи TTL и cleanup.
- Протестируй wizard back/reset behavior.
- Протестируй process restart behavior.

## Хэндлеры

- Держи handlers thin.
- Переноси business logic в application services.
- Передавай `CancellationToken` в I/O.
- Держи template и regex routes readable.
- Избегай слишком широких catch-all handlers.

## Callbacks

- Keep callback payloads compact.
- Не клади sensitive data в callback data.
- Answer callbacks manually или настрой auto-answer осознанно.
- Тестируй callback payload serialization.

## Ошибки

- Обрабатывай known business exceptions.
- Unknown exceptions должны оставаться visible.
- Логируй exception type и handler.
- Тестируй error handlers, которые влияют на users.

## Наблюдаемость

- Enable structured logs.
- Track handler duration.
- Track Telegram request count and latency.
- Track storage latency.
- Alert on repeated handler failures.

## Безопасность

- Храни bot token вне source code.
- Rotate bot token intentionally.
- Используй webhook secret token.
- Валидируй admin/user ids из configuration.
- Не пиши secrets в production logs.
- Не клади credentials или privileged actions в callback data.

## CI

- Build all projects.
- Run tests.
- Run package smoke tests.
- Keep analyzer diagnostics visible.
- Check documentation links.
