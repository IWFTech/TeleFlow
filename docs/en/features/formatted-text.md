# Formatted Text And Custom Emoji

Use the formatted-text builders for normal Telegram messages with names, links,
balances, spoilers, code, or custom emoji. Builders escape plain values and
render one explicit Bot API parse mode.

```csharp
using TeleFlow.Telegram.Formatting;

var text = TelegramHtml.Create()
    .Text("Balance: ")
    .Bold(balance.ToString("N0") + " BB")
    .LineBreak()
    .Link(profileUrl, "Open profile")
    .Text(" ")
    .CustomEmoji(
        customEmojiId: "5368324170671202286",
        fallbackEmoji: "💎")
    .Build();

await ctx.Message.AnswerAsync(text, ct);
```

The result is `TelegramFormattedText`. It contains rendered `Text` and an
explicit `TelegramParseMode`, so it never relies on
`TelegramBotDefaults.ParseMode`.

## Choose A Mode Explicitly

```csharp
var html = TelegramHtml.Create()
    .Text("Status: ")
    .Bold("active")
    .Build();

var markdown = TelegramMarkdownV2.Create()
    .Text("Status: ")
    .Bold("active")
    .Build();
```

HTML and MarkdownV2 are different formats. TeleFlow neither detects a format
from a string nor converts between formats. An HTML formatted value cannot be
appended to a MarkdownV2 builder, and vice versa.

## Safe Text And Composition

`Text(...)` and every normal text argument escape their input. Pass user names,
database values, and external text directly:

```csharp
var text = TelegramHtml.Create()
    .Text(player.DisplayName)
    .Text(" joined the chat.")
    .Build();
```

Do not escape a value yourself first. The builder preserves formatted fragments
during composition, so nested content is escaped exactly once:

```csharp
var text = TelegramHtml.Create()
    .Bold(content => content
        .Text("Active ")
        .Spoiler(secretValue))
    .Build();
```

The safe vocabulary is `Text`, `LineBreak`, `Bold`, `Italic`, `Underline`,
`Strikethrough`, `Spoiler`, `Code`, `Pre`, `Link`, `Mention`,
`BlockQuote`, and `CustomEmoji`.

`Code`, `Pre`, links, mentions, custom emoji, and block quotes receive plain
content rather than nested builders. Telegram does not allow arbitrary nested
entities in all of these constructs, so the API does not pretend otherwise.

```csharp
var text = TelegramMarkdownV2.Create()
    .BlockQuote("This is a quoted note.", expandable: true)
    .LineBreak()
    .Code("dotnet test")
    .LineBreak()
    .Pre("Console.WriteLine(\"Hello\");", language: "csharp")
    .Build();
```

## Links, Mentions, And Custom Emoji

```csharp
var text = TelegramHtml.Create()
    .Link(new Uri("https://example.com/profile/42"), "Profile")
    .Text(" for ")
    .Mention(123456789, "Alice")
    .LineBreak()
    .CustomEmoji(customEmojiId, fallbackEmoji: "💎")
    .Build();
```

The builder requires an absolute URI and a positive user id. Custom emoji also
require a fallback emoji. Telegram may show that fallback in notifications,
forwarded messages, unsupported clients, or destinations where the bot/account
cannot render the custom emoji. TeleFlow neither calls
`getCustomEmojiStickers` implicitly nor promises availability.

Custom emoji inside **inline keyboard buttons** are a different Bot API field:

```csharp
var keyboard = InlineKeyboardBuilder.Create()
    .Button(
        "Open",
        new OpenProfileCallback(),
        new InlineKeyboardButtonOptions
        {
            IconCustomEmojiId = customEmojiId
        })
    .Build();
```

## Sending And Editing

Framework context helpers accept `TelegramFormattedText`:

```csharp
await ctx.Message.AnswerAsync(text, ct);
await ctx.Message.ReplyAsync(text, ct);
await ctx.Callback.EditTextAsync(text, ct);

await ctx.Message.AnswerAsync(text, keyboard, ct);
await ctx.Callback.EditTextAsync(text, keyboard, ct);
```

These overloads forward the value's explicit parse mode through the normal
generated-client path. Existing `string` overloads retain their existing
behavior, including a configured client default parse mode.

For reply keyboards, force replies, captions, explicit `MessageEntity` values,
or another advanced request field, use the generated client directly:

```csharp
await ctx.Bot.SendMessageAsync(
    chatId: IntegerString.From(ctx.TelegramChat.Id),
    text: text.Text,
    parseMode: text.ParseMode,
    cancellationToken: ct);
```

## Unsafe Markup

`UnsafeMarkup` is only for static reviewed templates:

```csharp
var text = TelegramHtml.UnsafeMarkup(
    "<b>Static reviewed copy</b>");
```

It bypasses escaping and structural validation. Never pass user-controlled,
database-controlled, or external text to it.

## Entities And Rich Messages

Builders target ordinary Bot API `text` plus `parse_mode`. They do not replace
explicit `MessageEntity` values. They also do not create Bot API **Rich
Messages** (`InputRichMessage`, blocks, media, drafts, tables, or formulas).
That is a separate Telegram API surface and intentionally remains outside this
helper.
