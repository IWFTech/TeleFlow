# Контракт роутинга

Эта страница фиксирует наблюдаемые правила роутинга TeleFlow. Она дополняет
[руководство по handlers](../fundamentals/handlers-and-routing.md).

## Порядок выбора

Для message update TeleFlow сначала пробует command routes, затем message routes.
Если command не сматчилась, update всё ещё может попасть в `[Message]`, `[Text]`,
template или regex handler.

Внутри семейства routes выбор детерминирован:

| Правило | Кто выбирается раньше |
| --- | --- |
| State | Handler текущего state раньше stateless handler |
| Тип command route | Exact command, затем command template, затем command regex |
| Тип message route | Exact text, затем text template, text regex и bare message handler |
| Специфичность route | Более специфичный route того же типа |
| Последний tie-breaker | Порядок регистрации |

Filters запускаются после совпадения формы route. Если filter отклонил candidate,
TeleFlow продолжает поиск следующего candidate; это не error. Typed callback routes
идут раньше raw `[Callback]` fallback. Для chat-member route до filters должны
совпасть и тип update, и объявленный transition.

## Command prefixes

`CommandPrefixMode.Required` принимает только настроенные prefixes.
`CommandPrefixMode.Optional` сначала пытается найти настроенный prefix, затем
command text без prefix. `CommandPrefixMode.NoPrefix` принимает только text без
prefix.

Exact prefix-less command должна занять всё сообщение. Поэтому
`[Command("help", PrefixMode = CommandPrefixMode.Optional)]` сматчит `help`,
но не `help please`. У prefixed exact command могут быть аргументы, поэтому
`/help please` всё ещё попадёт в handler `help`. `AllowSpaceAfterPrefix`
определяет, допустим ли пробел после настроенного prefix.

### Пересекающиеся prefixes

Если prefixes пересекаются, выигрывает самый длинный совпавший prefix,
независимо от порядка объявления:

```csharp
[Command("confirm", Prefixes = new[] { "!", "!!" })]
public Task Confirm(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Confirmed.", ct);
}
```

`!!confirm` использует `!!`, даже если `!` указан первым. Prefixes
нормализуются один раз при построении route metadata, а не на каждом update.

## Slash command mentions

Команда в Telegram group может содержать mention бота, например
`/start@my_bot`. TeleFlow принимает её, только если `my_bot` - это текущий
бот. `/start@another_bot` никогда не считается `/start`.

Укажи `BotUsername`, если username известен на этапе конфигурации:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.BotUsername = "my_bot";
});
```

В обычных приложениях с `AddLongPolling()` и `AddWebhook()` TeleFlow один раз
вызывает `getMe` до запуска transport, если `BotUsername` не указан. Username
кэшируется на время жизни процесса. Если identity не удалось разрешить, hosted
transport не считается готовым. Роутинг никогда не делает Telegram I/O во время
обработки update.

В custom/direct update pipeline без TeleFlow transport укажи `BotUsername`,
если нужны commands с mention. Обычная `/start` bot identity не требует.

## Templates, regexes и text

`[TextTemplate]` и `[CommandTemplate]` TeleFlow автоматически anchor-ит:
весь relevant text или command body должен соответствовать template. Некорректное
optional или typed value просто означает, что route не совпала и можно попробовать
следующий candidate.

`[TextRegex]` и `[CommandRegex]` сохраняют regex приложения. Добавь `^` и
`$`, если route должна покрыть весь input.

Exact command/text comparisons и template matching используют `IgnoreCase`
route с ordinal, culture-independent семантикой. TeleFlow не нормализует
глобально incoming text и не меняет text, доступный в contexts. Unicode
normalization остаётся отдельным решением, основанным на воспроизводимом кейсе.

## Паритет регистрации

Generated registration через `AddTelegramHandlersFromAssembly(...)` и явный
`AddTelegramHandler<T>()` используют одну runtime route table и один selector.
Generated path - рекомендуемый default приложения; явная регистрация остаётся
полезной для узких modules и tests.
