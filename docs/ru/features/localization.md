# Локализация через Fluent

TeleFlow даёт локализацию двумя optional-пакетами:

- `IWF.TeleFlow.Framework.I18n` один раз определяет locale для каждого Telegram update;
- `IWF.TeleFlow.Framework.I18n.Fluent` загружает Project Fluent (`.ftl`) и форматирует сообщения через Linguini.

Обычному приложению достаточно Fluent-пакета: базовый пакет подтянется транзитивно.

```bash
dotnet add package IWF.TeleFlow.Framework.I18n.Fluent --prerelease
```

Локализация не входит в core package graph. Если пакет не установлен, приложение не получает зависимость от Linguini и не платит за i18n в runtime.

## Структура ресурсов

Создай отдельную директорию для каждой locale. Большой каталог дели по фичам:

```text
Locales/
  en/
    common/
      shared.ftl
    wallet/
      messages.ftl
  ru/
    common/
      shared.ftl
    wallet/
      messages.ftl
```

Скопируй ресурсы в output:

```xml
<ItemGroup>
  <Content Include="Locales\**\*.ftl"
           CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Регистрируй Fluent после `AddTelegramBot(...)`:

```csharp
using TeleFlow.Telegram.I18n;
using TeleFlow.Telegram.I18n.Fluent;

builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddTelegramFluentI18n(options =>
{
    options.ResourcesPath = "Locales";
    options.FallbackLocale = new Locale("en");
});
```

TeleFlow читает и парсит все ресурсы во время runtime validation до обработки первого update. Отсутствующая директория, сломанный FTL, duplicate messages, пустой locale catalog или отсутствующий fallback catalog останавливают startup. После проверки каталог immutable и безопасен для concurrent updates. Форматирование не читает файлы и не парсит FTL.

Все `.ftl` внутри директории одной locale загружаются рекурсивно в единый Fluent bundle. Messages и terms могут ссылаться на определения из других файлов и вложенных директорий этой locale; порядок файлов не влияет на разрешение ссылок. Идентификаторы должны быть уникальны в пределах всей locale. `$variables` не являются глобальными значениями из ресурсов: application code передаёт их при форматировании.

Hot reload в этой версии не поддерживается. После изменения каталога перезапусти приложение.

## Messages, variables, plurals, terms и attributes

Fluent остаётся единственным template language. TeleFlow не добавляет второй синтаксис placeholders:

```ftl
-wallet-emoji = <tg-emoji emoji-id="5368324170671202286">💎</tg-emoji>

wallet-summary =
    { -wallet-emoji } <b>Баланс:</b> { NUMBER($balance, minimumFractionDigits: 2) } BB
    { $items ->
        [one] Один предмет
        [few] { NUMBER($items) } предмета
       *[other] { NUMBER($items) } предметов
    }

profile-tabs =
    .overview = Обзор
    .inventory = Инвентарь
```

Для attributes используй `message-id.attribute`:

```csharp
var inventoryLabel = localizer.Format("profile-tabs.inventory");
```

Неизвестные messages, attributes, variables и functions приводят к `FluentLocalizationException`. TeleFlow не показывает пользователю key вместо перевода.

Adapter намеренно предоставляет небольшой набор функций поверх корректного locale-aware форматирования .NET, а не пытается повторить все параметры JavaScript `Intl`:

- `NUMBER(value, minimumFractionDigits, maximumFractionDigits, useGrouping)`;
- `DATETIME(value, dateStyle, timeStyle)` со стилями `short` и `long`.

Для вывода чисел используй `NUMBER(...)`. Так plural selection получает numeric value, а пользователь видит locale-aware представление. Raw numeric placeable подходит как selector, но сам по себе не обещает locale-specific форматирование.

`DATETIME(...)` принимает `DateTime`, `DateTimeOffset`, `DateOnly` и `TimeOnly`. Без явных styles используется обычный culture-specific формат .NET для соответствующего типа. Комбинации `short` и `long` отображаются в .NET patterns `g`, `G`, `f` и `F`, поэтому порядок и разделители остаются корректными для locale. Locale не определяет часовой пояс: application code должен заранее перевести значение в часовой пояс пользователя.

## Использование в handlers

Constructor injection рекомендуется, когда у handler несколько локализованных ответов:

```csharp
public sealed class WalletHandler(IFluentLocalizer localizer)
{
    [Command("wallet")]
    public Task Handle(MessageContext ctx, CancellationToken ct)
    {
        var text = localizer.FormatHtml(
            "wallet-summary",
            ("balance", 1_250.5),
            ("items", 3));

        return ctx.Message.AnswerAsync(text, ct);
    }
}
```

Generated handler parameter injection подходит для разового использования:

```csharp
[Command("language")]
public Task Language(
    MessageContext ctx,
    IFluentLocalizer localizer,
    CancellationToken ct)
{
    return ctx.Message.AnswerAsync(localizer.Format("language-current"), ct);
}
```

У Telegram contexts есть короткие presentation helpers:

```csharp
var plain = ctx.I18n("button-cancel");
var html = ctx.I18nHtml("wallet-summary", ("balance", balance), ("items", items));
var markdown = ctx.I18nMarkdownV2("wallet-summary", ("balance", balance));
```

`ctx.I18n(...)` не выполняет I/O: он резолвит уже scoped `IFluentLocalizer`. Domain services не должны зависеть от Telegram contexts; подключай локализацию на presentation boundary.

## Определение locale

Locale определяется один раз до handler dispatch:

1. application resolvers в порядке регистрации;
2. Telegram `User.LanguageCode`;
3. настроенная fallback locale.

Catalog выбирается как exact locale, parent locale, затем fallback. Например, `ru-UA` использует `ru`, если `Locales/ru-UA` отсутствует.

Для сохранённой настройки пользователя или чата добавь scoped resolver:

```csharp
public sealed class PlayerLocaleResolver(IPlayerPreferences preferences) : ILocaleResolver
{
    public async ValueTask<Locale?> TryResolveAsync(
        LocaleResolutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.User is null)
        {
            return null;
        }

        var locale = await preferences.GetLocaleAsync(
            context.User.Id,
            cancellationToken);

        return Locale.TryCreate(locale, out var result) ? result : null;
    }
}

builder.Services.AddTelegramLocaleResolver<PlayerLocaleResolver>();
```

`null` продолжает resolver chain. Ошибка PostgreSQL, Redis или repository не маскируется fallback locale: она идёт в обычный TeleFlow update error path.

Locale middleware добавляется вызовом `AddTelegramFluentI18n(...)`. Custom middleware, которому нужен `IFluentLocalizer`, регистрируй после него:

```csharp
builder.Services.AddTelegramFluentI18n(...);
builder.Services.AddUpdateMiddleware<LocalizedPolicyMiddleware>();
```

Middleware и resolvers scoped, поэтому в их constructor можно передавать scoped repositories, DbContext и Redis services.

## Plain text, HTML и MarkdownV2

Destination выбирается явно:

- `Format` / `I18n` возвращает plain `string` для labels, callback alerts, logs и plain messages;
- `FormatHtml` / `I18nHtml` возвращает `TelegramFormattedText` с HTML parse mode;
- `FormatMarkdownV2` / `I18nMarkdownV2` возвращает `TelegramFormattedText` с MarkdownV2 parse mode.

Markup внутри source-controlled FTL считается доверенным application content. Dynamic arguments автоматически экранируются под выбранный mode. При HTML interpolation также экранируются кавычки, поэтому значение не может выйти за границы quoted attribute:

```ftl
welcome = <b>Привет, { $name }</b>
```

`("name", "<admin>")` в `FormatHtml` даст `<b>Привет, &lt;admin&gt;</b>`.

Экранирование защищает структуру markup, но не проверяет смысл URL, custom emoji ID или другого Telegram value. Динамические links, mentions и custom emoji собирай через `TelegramHtml.Create()` или `TelegramMarkdownV2.Create()`, а затем передавай готовый `TelegramFormattedText` одним typed argument. Так destination validation остаётся в коде, а не в переводах.

Готовый проверенный `TelegramFormattedText` fragment можно вставить без double escaping, если parse mode совпадает:

```csharp
var emoji = TelegramHtml.Create()
    .CustomEmoji(customEmojiId, "💎")
    .Build();

var text = ctx.I18nHtml("wallet-title", ("emoji", emoji));
```

HTML внутри MarkdownV2, MarkdownV2 внутри HTML и formatted text внутри plain mode падают до Bot API request. Custom emoji в inline keyboard остаётся `InlineKeyboardButtonOptions.IconCustomEmojiId`; i18n локализует label, а не поле icon.

## Rich Messages и LaTeX

LaTeX держи в application code и передавай как экранированный argument. Переводчик управляет пояснением и расположением формулы, но FTL braces не конфликтуют с LaTeX:

```ftl
game-formula =
    <h2>Формула игры</h2>
    <tg-math-block>{ $formula }</tg-math-block>
```

```csharp
var content = formatter.FormatHtml(
    locale,
    "game-formula",
    ("formula", BuildFormula(snapshot)));

var richMessage = new InputRichMessage { Html = content.Text };
```

## Background jobs и outbox

Ambient locale и `AsyncLocal` нет. Вне update processing locale указывается явно:

```csharp
var text = formatter.FormatHtml(
    new Locale(player.Locale),
    "daily-reward",
    ("amount", reward));
```

Инжектируй `IFluentTextFormatter` в background services, startup tasks, broadcasts и outbox dispatchers. Application сам выбирает: сохранить уже отрендеренный текст либо сохранить message key, locale и serializable arguments для рендера при отправке.

## Performance и production

- Resolver chain обращается к locale storage не больше одного раза на update.
- После locale middleware catalog lookup и formatting синхронны.
- В handlers нет FTL file I/O и parsing.
- Linguini создаёт внутренний formatting scope и argument dictionaries для вызовов с arguments. TeleFlow убирает reflection и пользовательские dictionaries, но не обещает zero allocation.
- Не помещай secrets, tokens и user content в message identifiers. Exceptions содержат message ID и locale, но не argument values и не переведённый текст.
- Регистрируй localization до middleware, которому нужны переводы, и проверяй fallback catalog при deployment verification.
