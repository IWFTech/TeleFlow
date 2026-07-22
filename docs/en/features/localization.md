# Localization With Fluent

TeleFlow provides localization as two optional packages:

- `IWF.TeleFlow.Framework.I18n` resolves one locale for each Telegram update;
- `IWF.TeleFlow.Framework.I18n.Fluent` loads Project Fluent (`.ftl`) resources and formats messages through Linguini.

Most applications install only the Fluent package. It brings the base package transitively:

```bash
dotnet add package IWF.TeleFlow.Framework.I18n.Fluent --prerelease
```

Localization is not part of the core framework graph. Applications that do not install these packages pay no localization dependency or runtime cost.

## Resource Layout

Keep one directory per locale. Split files by feature when the catalog grows:

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

Copy resources to the output directory:

```xml
<ItemGroup>
  <Content Include="Locales\**\*.ftl"
           CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Register Fluent after `AddTelegramBot(...)`:

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

TeleFlow reads and parses every resource during runtime validation, before the first update is handled. Missing directories, invalid FTL, duplicate messages, empty locale catalogs, and an absent fallback catalog stop startup. The resulting catalog is immutable and shared safely by concurrent updates. Formatting does not read files or parse FTL.

All `.ftl` files under one locale directory are loaded recursively into a single Fluent bundle. Messages and terms may reference definitions from other files and nested directories in that locale; file order does not affect reference resolution. Identifiers must be unique across the entire locale. `$variables` are not global resource values: application code supplies them when formatting a message.

Hot reload is not supported in this release. Restart the application after changing a catalog.

## Messages, Variables, Plurals, Terms, And Attributes

Fluent remains the only template language. TeleFlow does not add another placeholder syntax:

```ftl
-wallet-emoji = <tg-emoji emoji-id="5368324170671202286">💎</tg-emoji>

wallet-summary =
    { -wallet-emoji } <b>Balance:</b> { NUMBER($balance, minimumFractionDigits: 2) } BB
    { $items ->
        [one] One item
       *[other] { NUMBER($items) } items
    }

profile-tabs =
    .overview = Overview
    .inventory = Inventory
```

Use `message-id.attribute` for message attributes:

```csharp
var inventoryLabel = localizer.Format("profile-tabs.inventory");
```

Unknown messages, attributes, variables, or functions throw `FluentLocalizationException`. TeleFlow never returns the missing key as user-visible text.

The adapter provides a deliberately small .NET-backed function surface rather than attempting to reproduce every JavaScript `Intl` option:

- `NUMBER(value, minimumFractionDigits, maximumFractionDigits, useGrouping)`;
- `DATETIME(value, dateStyle, timeStyle)` with `short` and `long` styles.

Use `NUMBER(...)` when rendering numeric values. It preserves Fluent plural selection while producing locale-aware output. Raw numeric placeables are useful as selectors, but do not promise locale-specific presentation by themselves.

`DATETIME(...)` accepts `DateTime`, `DateTimeOffset`, `DateOnly`, and `TimeOnly`. Without explicit styles, it uses the normal culture-specific .NET format for that value. Combined `short` and `long` styles map to the .NET `g`, `G`, `f`, and `F` patterns, preserving culture-specific order and separators. A locale does not define a time zone: convert the value to the user's time zone in application code before formatting it.

## Handler Usage

Constructor injection is the recommended path when a handler has several localized responses:

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

Generated handler parameter injection works for one-off use:

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

Telegram contexts also expose concise presentation helpers:

```csharp
var plain = ctx.I18n("button-cancel");
var html = ctx.I18nHtml("wallet-summary", ("balance", balance), ("items", items));
var markdown = ctx.I18nMarkdownV2("wallet-summary", ("balance", balance));
```

`ctx.I18n(...)` performs no I/O. It resolves the already scoped `IFluentLocalizer`. Keep domain services independent of Telegram contexts and inject localization only at presentation boundaries.

## Locale Resolution

Locale resolution runs once before handler dispatch:

1. custom application resolvers, in registration order;
2. Telegram `User.LanguageCode`;
3. the configured fallback locale.

Requested catalogs resolve as exact locale, parent locale, then fallback. For example, `ru-UA` uses `ru` when `Locales/ru-UA` is absent.

Use a custom scoped resolver for a persisted user or chat preference:

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

Returning `null` continues the resolver chain. Storage failures are not converted into fallback decisions: they propagate through the ordinary TeleFlow update error path.

Locale middleware is inserted when `AddTelegramFluentI18n(...)` is called. Register custom middleware that needs `IFluentLocalizer` after that call:

```csharp
builder.Services.AddTelegramFluentI18n(...);
builder.Services.AddUpdateMiddleware<LocalizedPolicyMiddleware>();
```

Both middleware and resolvers are scoped, so PostgreSQL, Redis, repositories, and other scoped dependencies are valid constructor dependencies.

## Plain Text, HTML, And MarkdownV2

Choose the destination explicitly:

- `Format` / `I18n` returns plain `string` for button labels, callback alerts, logs, and plain messages;
- `FormatHtml` / `I18nHtml` returns `TelegramFormattedText` with HTML parse mode;
- `FormatMarkdownV2` / `I18nMarkdownV2` returns `TelegramFormattedText` with MarkdownV2 parse mode.

Markup stored in source-controlled FTL is trusted application content. Dynamic arguments are escaped automatically for the selected mode. HTML interpolation also encodes quote delimiters, so a value cannot leave a quoted attribute:

```ftl
welcome = <b>Hello, { $name }</b>
```

Passing `("name", "<admin>")` to `FormatHtml` produces `<b>Hello, &lt;admin&gt;</b>`.

Escaping protects markup structure; it does not validate the meaning of a URL, custom emoji ID, or another Telegram value. Build dynamic links, mentions, and custom emoji through `TelegramHtml.Create()` or `TelegramMarkdownV2.Create()`, then pass the resulting `TelegramFormattedText` as one typed argument. This keeps destination validation in code instead of translations.

A reviewed `TelegramFormattedText` fragment can be inserted without double escaping when its parse mode matches:

```csharp
var emoji = TelegramHtml.Create()
    .CustomEmoji(customEmojiId, "💎")
    .Build();

var text = ctx.I18nHtml("wallet-title", ("emoji", emoji));
```

Passing HTML to MarkdownV2, MarkdownV2 to HTML, or formatted text to plain mode fails before a Bot API request. Inline-keyboard custom emoji remain `InlineKeyboardButtonOptions.IconCustomEmojiId`; localization owns the button label, not the button icon field.

## Rich Messages And LaTeX

Keep LaTeX in application code and pass it as an escaped argument. Translators control the explanation and formula position without fighting Fluent braces:

```ftl
game-formula =
    <h2>Game formula</h2>
    <tg-math-block>{ $formula }</tg-math-block>
```

```csharp
var content = formatter.FormatHtml(
    locale,
    "game-formula",
    ("formula", BuildFormula(snapshot)));

var richMessage = new InputRichMessage { Html = content.Text };
```

## Background Work And Outbox

There is no ambient locale and no `AsyncLocal`. Code outside update processing must choose explicitly:

```csharp
var text = formatter.FormatHtml(
    new Locale(player.Locale),
    "daily-reward",
    ("amount", reward));
```

Inject `IFluentTextFormatter` into background services, startup tasks, broadcasts, and outbox dispatchers. Decide at the application boundary whether to store final rendered text or store a message key, locale, and serializable arguments for later rendering.

## Performance And Production Notes

- Locale storage is queried at most once per update by the registered resolver chain.
- Catalog lookup and formatting are synchronous after locale middleware.
- No FTL file I/O or parsing occurs in handlers.
- Linguini creates internal formatting scope and argument dictionaries for calls with arguments. TeleFlow avoids reflection and user-created dictionaries, but does not claim zero allocation.
- Do not put secrets, tokens, or user content in message identifiers. Exceptions include message ID and locale, never argument values or translated content.
- Register localization before middleware that needs localized text, and test fallback catalogs during deployment verification.
